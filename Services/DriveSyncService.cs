using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Tirki.Models;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Tirki.Services;

/// <summary>
/// Sincronizza transazioni e categorie con Drive usando un file per mese ("transactions_yyyy-MM.json")
/// invece di un unico blob con tutto lo storico, più un piccolo "manifest.json" che elenca, per ogni
/// mese, l'UpdatedAt più recente che conteneva all'ultimo upload. Così un device scopre con un solo
/// download quali mesi sono cambiati (qui o altrove) e tocca solo quelli — niente re-download/upload
/// dell'intera storia ad ogni sync. Le categorie restano in un unico file piccolo, sincronizzato sempre.
///
/// Ogni sync procede in due fasi separate (mai intrecciate): prima si scarica tutto ciò che serve e si
/// aggiorna il database locale (fonte di verità), poi si ricalcola il raggruppamento per mese dal
/// database appena aggiornato e si carica. Questo evita di "resuscitare" una transazione nel mese
/// vecchio quando la sua data viene modificata in un mese diverso: se sia il mese di origine sia quello
/// di destinazione vengono scaricati PRIMA di qualunque upload, il database locale riflette già la
/// posizione corretta quando si genera il contenuto da caricare.
/// </summary>
public class DriveSyncService
{
    private const string AppFolderName = "Tirki";
    private const string ManifestFileName = "manifest.json";
    private const string CategoriesFileName = "categories.json";
    private const string LegacyDataFileName = "tirki_data.json";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private const string PendingDirtyMonthsKey = "sync_pending_dirty_months";
    private const string KnownMonthWatermarksKey = "sync_known_month_watermarks";

    private readonly GoogleAuthService _auth;
    private readonly LocalDatabaseService _database;

    public DriveSyncService(GoogleAuthService auth, LocalDatabaseService database)
    {
        _auth = auth;
        _database = database;
    }

    private static string MonthKey(DateTime date) => date.ToString("yyyy-MM");

    private static string MonthFileName(string monthKey) => $"transactions_{monthKey}.json";

    /// <summary>
    /// Da chiamare ogni volta che una transazione viene salvata o cancellata, così il prossimo sync
    /// sa quali file mensili toccare. Passare <paramref name="previousDate"/> quando si modifica una
    /// transazione esistente: se la data cambia mese, viene marcato "sporco" anche il mese di origine.
    /// </summary>
    public void MarkTransactionDirty(Transaction transaction, DateTime? previousDate = null)
    {
        var months = GetPendingDirtyMonths();
        months.Add(MonthKey(transaction.Date));
        if (previousDate.HasValue)
            months.Add(MonthKey(previousDate.Value));
        SavePendingDirtyMonths(months);
    }

    public async Task SyncNowAsync()
    {
        var drive = await CreateDriveServiceAsync();
        var folderId = await GetOrCreateFolderAsync(drive);

        // Categorie: file unico piccolo, sincronizzato per intero come prima.
        var categoriesFileId = await GetOrCreateNamedFileAsync(drive, folderId, CategoriesFileName, "[]");
        var remoteCategories = await DownloadListAsync<Category>(drive, categoriesFileId);
        var localCategories = await _database.GetAllCategoriesRawAsync();
        var mergedCategories = MergeById(localCategories, remoteCategories, c => c.Id, c => c.UpdatedAt);
        foreach (var category in mergedCategories)
            await _database.SaveCategoryRawAsync(category);
        await UploadListAsync(drive, categoriesFileId, mergedCategories);

        // Transazioni: manifest + file per mese.
        var manifestFileId = await GetOrCreateNamedFileAsync(drive, folderId, ManifestFileName, "{}");
        var remoteManifest = await DownloadManifestAsync(drive, manifestFileId);

        await MigrateLegacyFileIfNeededAsync(drive, folderId, remoteManifest);

        var localTransactions = await _database.GetAllTransactionsRawAsync();
        var localById = localTransactions.ToDictionary(t => t.Id);
        var localMonthsKnown = localTransactions.Select(t => MonthKey(t.Date)).ToHashSet();

        var dirtyMonths = GetPendingDirtyMonths();
        var knownWatermarks = GetKnownMonthWatermarks();

        var monthsToSync = new HashSet<string>(dirtyMonths);
        foreach (var (month, remoteWatermark) in remoteManifest.MonthWatermarks)
        {
            var isKnown = knownWatermarks.TryGetValue(month, out var known);
            if (!isKnown || remoteWatermark > known)
                monthsToSync.Add(month);
        }
        // Mesi che esistono solo in locale (mai ancora comparsi nel manifest): primo upload da questo device.
        foreach (var month in localMonthsKnown)
        {
            if (!remoteManifest.MonthWatermarks.ContainsKey(month) && !knownWatermarks.ContainsKey(month))
                monthsToSync.Add(month);
        }

        // Fase 1 — scarica tutto ciò che serve e aggiorna il database locale (fonte di verità).
        var monthFileIds = new Dictionary<string, string>();
        foreach (var month in monthsToSync)
        {
            var fileId = await GetOrCreateNamedFileAsync(drive, folderId, MonthFileName(month), "[]");
            monthFileIds[month] = fileId;

            var remoteMonthTransactions = await DownloadListAsync<Transaction>(drive, fileId);
            foreach (var remote in remoteMonthTransactions)
            {
                if (!localById.TryGetValue(remote.Id, out var local) || remote.UpdatedAt > local.UpdatedAt)
                {
                    await _database.SaveTransactionRawAsync(remote);
                    localById[remote.Id] = remote;
                }
            }
        }

        // Fase 2 — ricalcola il raggruppamento per mese dal database locale ORA aggiornato.
        var refreshedLocal = await _database.GetAllTransactionsRawAsync();
        var refreshedByMonth = refreshedLocal
            .GroupBy(t => MonthKey(t.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Fase 3 — carica ogni mese toccato: il database locale è ormai autorevole, niente merge da rifare.
        var updatedWatermarks = new Dictionary<string, DateTime>(remoteManifest.MonthWatermarks);
        foreach (var month in monthsToSync)
        {
            var monthTransactions = refreshedByMonth.TryGetValue(month, out var list) ? list : new List<Transaction>();
            await UploadListAsync(drive, monthFileIds[month], monthTransactions);

            var watermark = monthTransactions.Count > 0 ? monthTransactions.Max(t => t.UpdatedAt) : DateTime.UtcNow;
            updatedWatermarks[month] = watermark;
            knownWatermarks[month] = watermark;
        }

        // Il manifest è un unico file: se un altro device ha scritto la sua versione nel frattempo
        // (es. durante un primo sync storico lungo che si sovrappone a una modifica rapida altrove),
        // scriverlo semplicemente sovrascriverebbe i suoi mesi con lo snapshot ORMAI VECCHIO scaricato
        // a inizio funzione. Per questo si riscarica una copia fresca appena prima di scrivere e si
        // unisce mese per mese (vince il watermark più recente) invece di sovrascrivere alla cieca.
        var freshRemoteManifest = await DownloadManifestAsync(drive, manifestFileId);
        foreach (var (month, watermark) in freshRemoteManifest.MonthWatermarks)
        {
            if (!updatedWatermarks.TryGetValue(month, out var existing) || watermark > existing)
                updatedWatermarks[month] = watermark;
        }

        await UploadManifestAsync(drive, manifestFileId, new SyncManifest { MonthWatermarks = updatedWatermarks });
        SaveKnownMonthWatermarks(knownWatermarks);
        SavePendingDirtyMonths(new HashSet<string>());
    }

    /// <summary>
    /// Se il manifest è ancora vuoto (nessun sync col nuovo formato è mai avvenuto) e su Drive esiste
    /// ancora il vecchio file unico "tirki_data.json", lo scarica e ne unisce il contenuto nel database
    /// locale: il normale flusso di sync che segue penserà poi a ripartirlo nei file mensili. Il vecchio
    /// file non viene toccato né cancellato, resta come copia di sicurezza inerte.
    /// </summary>
    private async Task MigrateLegacyFileIfNeededAsync(DriveService drive, string folderId, SyncManifest remoteManifest)
    {
        if (remoteManifest.MonthWatermarks.Count > 0) return;

        var legacyFileId = await FindFileAsync(drive, folderId, LegacyDataFileName);
        if (legacyFileId is null) return;

        using var stream = new MemoryStream();
        await drive.Files.Get(legacyFileId).DownloadAsync(stream);
        if (stream.Length == 0) return;

        stream.Position = 0;
        var legacyPayload = await JsonSerializer.DeserializeAsync<SyncPayload>(stream) ?? new SyncPayload();

        var localTransactions = await _database.GetAllTransactionsRawAsync();
        var localCategories = await _database.GetAllCategoriesRawAsync();

        var mergedTransactions = MergeById(localTransactions, legacyPayload.Transactions, t => t.Id, t => t.UpdatedAt);
        var mergedCategories = MergeById(localCategories, legacyPayload.Categories, c => c.Id, c => c.UpdatedAt);

        foreach (var transaction in mergedTransactions)
            await _database.SaveTransactionRawAsync(transaction);
        foreach (var category in mergedCategories)
            await _database.SaveCategoryRawAsync(category);
    }

    private async Task<DriveService> CreateDriveServiceAsync()
    {
        var accessToken = await _auth.GetAccessTokenAsync();
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Tirki",
        });
    }

    private static List<T> MergeById<T>(
        List<T> local,
        List<T> remote,
        Func<T, Guid> idSelector,
        Func<T, DateTime> updatedAtSelector)
    {
        var byId = new Dictionary<Guid, T>();
        foreach (var item in local)
            byId[idSelector(item)] = item;

        foreach (var item in remote)
        {
            var id = idSelector(item);
            if (!byId.TryGetValue(id, out var existing) || updatedAtSelector(item) > updatedAtSelector(existing))
                byId[id] = item;
        }

        return byId.Values.ToList();
    }

    private static async Task<string> GetOrCreateFolderAsync(DriveService drive)
    {
        var listRequest = drive.Files.List();
        listRequest.Q = $"name = '{AppFolderName}' and mimeType = '{FolderMimeType}' and trashed = false";
        listRequest.Spaces = "drive";
        listRequest.Fields = "files(id)";
        var result = await listRequest.ExecuteAsync();

        var existing = result.Files?.FirstOrDefault();
        if (existing is not null) return existing.Id;

        var created = await drive.Files.Create(new DriveFile
        {
            Name = AppFolderName,
            MimeType = FolderMimeType,
        }).ExecuteAsync();

        return created.Id;
    }

    private static async Task<string?> FindFileAsync(DriveService drive, string folderId, string fileName)
    {
        var listRequest = drive.Files.List();
        listRequest.Q = $"name = '{fileName}' and '{folderId}' in parents and trashed = false";
        listRequest.Spaces = "drive";
        listRequest.Fields = "files(id, modifiedTime)";
        var result = await listRequest.ExecuteAsync();

        var matches = result.Files;
        if (matches is null || matches.Count == 0) return null;

#if ANDROID
        if (matches.Count > 1)
            Android.Util.Log.Warn("AutoSync", $"'{fileName}': {matches.Count} file duplicati trovati, uso il più recente");
#endif
        // Se esistono duplicati (creati da sync quasi simultanei prima di questo fix), usa sempre
        // il più aggiornato invece di uno arbitrario: altrimenti due device potrebbero leggere/scrivere
        // fisicamente file diversi con lo stesso nome e non convergere mai.
        return matches.OrderByDescending(f => f.ModifiedTimeDateTimeOffset).First().Id;
    }

    private static async Task<string> GetOrCreateNamedFileAsync(DriveService drive, string folderId, string fileName, string emptyContent)
    {
        var existingId = await FindFileAsync(drive, folderId, fileName);
        if (existingId is not null) return existingId;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(emptyContent));
        var createRequest = drive.Files.Create(
            new DriveFile { Name = fileName, Parents = new List<string> { folderId } },
            stream,
            "application/json");
        createRequest.Fields = "id";
        await createRequest.UploadAsync();

        if (createRequest.ResponseBody is null)
            throw new InvalidOperationException($"Creazione del file '{fileName}' su Drive non riuscita.");

        return createRequest.ResponseBody.Id;
    }

    private static async Task<List<T>> DownloadListAsync<T>(DriveService drive, string fileId)
    {
        using var stream = new MemoryStream();
        await drive.Files.Get(fileId).DownloadAsync(stream);

        if (stream.Length == 0) return new List<T>();

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<List<T>>(stream) ?? new List<T>();
    }

    private static async Task UploadListAsync<T>(DriveService drive, string fileId, List<T> items)
    {
        var json = JsonSerializer.Serialize(items);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var updateRequest = drive.Files.Update(new DriveFile(), fileId, stream, "application/json");
        await updateRequest.UploadAsync();
    }

    private static async Task<SyncManifest> DownloadManifestAsync(DriveService drive, string fileId)
    {
        using var stream = new MemoryStream();
        await drive.Files.Get(fileId).DownloadAsync(stream);

        if (stream.Length == 0) return new SyncManifest();

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<SyncManifest>(stream) ?? new SyncManifest();
    }

    private static async Task UploadManifestAsync(DriveService drive, string fileId, SyncManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var updateRequest = drive.Files.Update(new DriveFile(), fileId, stream, "application/json");
        await updateRequest.UploadAsync();
    }

    private static HashSet<string> GetPendingDirtyMonths()
    {
        var raw = Preferences.Default.Get(PendingDirtyMonthsKey, string.Empty);
        return string.IsNullOrEmpty(raw)
            ? new HashSet<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    private static void SavePendingDirtyMonths(HashSet<string> months)
        => Preferences.Default.Set(PendingDirtyMonthsKey, string.Join(",", months));

    private static Dictionary<string, DateTime> GetKnownMonthWatermarks()
    {
        var raw = Preferences.Default.Get(KnownMonthWatermarksKey, string.Empty);
        if (string.IsNullOrEmpty(raw)) return new Dictionary<string, DateTime>();
        return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(raw) ?? new Dictionary<string, DateTime>();
    }

    private static void SaveKnownMonthWatermarks(Dictionary<string, DateTime> watermarks)
        => Preferences.Default.Set(KnownMonthWatermarksKey, JsonSerializer.Serialize(watermarks));
}
