using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Tirki.Models;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Tirki.Services;

/// <summary>
/// Sincronizza transazioni e categorie con un file JSON su Google Drive
/// (cartella "Tirki", file "tirki_data.json"), fondendo lo stato locale con quello
/// remoto record per record: per ogni Id vince chi ha l'UpdatedAt più recente.
/// Le cancellazioni sono soft-delete (IsDeleted), mai rimosse fisicamente, così un
/// dispositivo offline non "resuscita" record cancellati altrove.
/// </summary>
public class DriveSyncService
{
    private const string AppFolderName = "Tirki";
    private const string DataFileName = "tirki_data.json";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private readonly GoogleAuthService _auth;
    private readonly LocalDatabaseService _database;

    public DriveSyncService(GoogleAuthService auth, LocalDatabaseService database)
    {
        _auth = auth;
        _database = database;
    }

    public async Task SyncNowAsync()
    {
        var drive = await CreateDriveServiceAsync();
        var folderId = await GetOrCreateFolderAsync(drive);
        var fileId = await GetOrCreateDataFileAsync(drive, folderId);

        var remote = await DownloadAsync(drive, fileId);

        var localTransactions = await _database.GetAllTransactionsRawAsync();
        var localCategories = await _database.GetAllCategoriesRawAsync();

        var mergedTransactions = MergeById(localTransactions, remote.Transactions, t => t.Id, t => t.UpdatedAt);
        var mergedCategories = MergeById(localCategories, remote.Categories, c => c.Id, c => c.UpdatedAt);

        foreach (var transaction in mergedTransactions)
            await _database.SaveTransactionRawAsync(transaction);
        foreach (var category in mergedCategories)
            await _database.SaveCategoryRawAsync(category);

        await UploadAsync(drive, fileId, new SyncPayload
        {
            Transactions = mergedTransactions,
            Categories = mergedCategories,
        });
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

    private static async Task<string> GetOrCreateDataFileAsync(DriveService drive, string folderId)
    {
        var listRequest = drive.Files.List();
        listRequest.Q = $"name = '{DataFileName}' and '{folderId}' in parents and trashed = false";
        listRequest.Spaces = "drive";
        listRequest.Fields = "files(id)";
        var result = await listRequest.ExecuteAsync();

        var existing = result.Files?.FirstOrDefault();
        if (existing is not null) return existing.Id;

        var emptyPayload = JsonSerializer.Serialize(new SyncPayload());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(emptyPayload));

        var createRequest = drive.Files.Create(
            new DriveFile { Name = DataFileName, Parents = new List<string> { folderId } },
            stream,
            "application/json");
        createRequest.Fields = "id";
        await createRequest.UploadAsync();

        if (createRequest.ResponseBody is null)
            throw new InvalidOperationException("Creazione del file su Drive non riuscita.");

        return createRequest.ResponseBody.Id;
    }

    private static async Task<SyncPayload> DownloadAsync(DriveService drive, string fileId)
    {
        using var stream = new MemoryStream();
        await drive.Files.Get(fileId).DownloadAsync(stream);

        if (stream.Length == 0) return new SyncPayload();

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<SyncPayload>(stream) ?? new SyncPayload();
    }

    private static async Task UploadAsync(DriveService drive, string fileId, SyncPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var updateRequest = drive.Files.Update(new DriveFile(), fileId, stream, "application/json");
        await updateRequest.UploadAsync();
    }
}
