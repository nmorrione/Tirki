using Tirki.Models;

namespace Tirki.Services;

public enum SyncOutcome
{
    Success,
    NotSignedIn,
    AlreadyInProgress,
    Failed,
}

/// <summary>
/// Coordina la sincronizzazione automatica con Drive (avvio app, resume da background,
/// dopo ogni scrittura con debounce) oltre al pulsante manuale "Sincronizza ora".
/// Un solo sync gira alla volta: richieste concorrenti vengono scartate silenziosamente.
/// </summary>
public class AutoSyncService
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PeriodicSyncInterval = TimeSpan.FromSeconds(30);
    private const string LastSyncAtKey = "last_sync_at_utc";

    private readonly GoogleAuthService _auth;
    private readonly DriveSyncService _driveSync;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private Timer? _periodicTimer;

    public AutoSyncService(GoogleAuthService auth, DriveSyncService driveSync)
    {
        _auth = auth;
        _driveSync = driveSync;
    }

    /// <summary>Notifica testo di stato leggibile dall'utente (es. per la pagina Impostazioni).</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Notifica silenziosa (nessun testo) ad ogni sync riuscito: usata per aggiornare la griglia in background.</summary>
    public event Action? SyncCompleted;

    public string? LastError { get; private set; }

    /// <summary>Ultimo stato di sync noto, letto da Preferences: sopravvive a restart e a pagine mai aperte durante il sync.</summary>
    public string? GetLastKnownStatus()
    {
        var stored = Preferences.Default.Get(LastSyncAtKey, string.Empty);
        if (string.IsNullOrEmpty(stored)) return null;

        var utc = DateTime.Parse(stored, null, System.Globalization.DateTimeStyles.RoundtripKind);
        return $"Ultima sincronizzazione: {utc.ToLocalTime():dd/MM/yyyy HH:mm}";
    }

    public async Task<SyncOutcome> SyncNowAsync()
    {
        if (!await _auth.IsSignedInAsync())
            return SyncOutcome.NotSignedIn;

        if (!await _syncLock.WaitAsync(0))
            return SyncOutcome.AlreadyInProgress;

        try
        {
            StatusChanged?.Invoke("Sincronizzazione in corso...");
            await _driveSync.SyncNowAsync();

            var nowUtc = DateTime.UtcNow;
            Preferences.Default.Set(LastSyncAtKey, nowUtc.ToString("o"));
            StatusChanged?.Invoke($"Ultima sincronizzazione: {nowUtc.ToLocalTime():dd/MM/yyyy HH:mm}");
            SyncCompleted?.Invoke();
            return SyncOutcome.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            StatusChanged?.Invoke("Sincronizzazione automatica non riuscita.");
            return SyncOutcome.Failed;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>Avvia un sync subito, senza attenderne l'esito (avvio app, resume).</summary>
    public void TriggerBackgroundSync() => _ = SyncNowAsync();

    /// <summary>
    /// Avvia un sync periodico in background (per intercettare modifiche fatte da altri device
    /// mentre l'app resta aperta). Da chiamare solo quando l'app è in primo piano.
    /// </summary>
    public void StartPeriodicSync()
    {
        _periodicTimer?.Dispose();
        _periodicTimer = new Timer(_ => TriggerBackgroundSync(), null, PeriodicSyncInterval, PeriodicSyncInterval);
    }

    /// <summary>Ferma il sync periodico: da chiamare quando l'app va in background.</summary>
    public void StopPeriodicSync()
    {
        _periodicTimer?.Dispose();
        _periodicTimer = null;
    }

    /// <summary>Segnala al motore di sync quale mese (ed eventualmente il mese di provenienza) è cambiato.</summary>
    public void MarkTransactionDirty(Transaction transaction, DateTime? previousDate = null)
        => _driveSync.MarkTransactionDirty(transaction, previousDate);

    /// <summary>
    /// Rimanda il sync di qualche secondo, riavviando il timer ad ogni chiamata:
    /// più scritture ravvicinate producono un solo sync invece di uno per ciascuna.
    /// </summary>
    public void TriggerDebouncedSync()
    {
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await SyncNowAsync();
        });
    }
}
