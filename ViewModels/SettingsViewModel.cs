using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Services;

namespace Tirki.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly HistoricalImportService _historicalImport;
    private readonly GoogleAuthService _auth;
    private readonly DriveSyncService _driveSync;

    public SettingsViewModel(HistoricalImportService historicalImport, GoogleAuthService auth, DriveSyncService driveSync)
    {
        _historicalImport = historicalImport;
        _auth = auth;
        _driveSync = driveSync;

        var savedTheme = Preferences.Default.Get(AppPreferenceKeys.Theme, nameof(AppTheme.Unspecified));
        _isInitializingTheme = true;
        IsLightTheme = savedTheme == nameof(AppTheme.Light);
        IsDarkTheme = savedTheme == nameof(AppTheme.Dark);
        IsSystemTheme = savedTheme == nameof(AppTheme.Unspecified);
        _isInitializingTheme = false;

        _ = RefreshSignInStateAsync();
    }

    private readonly bool _isInitializingTheme;

    public string AppVersion => $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    [ObservableProperty]
    private bool isLightTheme;

    [ObservableProperty]
    private bool isDarkTheme;

    [ObservableProperty]
    private bool isSystemTheme;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isSignedIn;

    [ObservableProperty]
    private string googleButtonText = "Accedi con Google";

    [ObservableProperty]
    private string syncStatus = string.Empty;

    partial void OnIsLightThemeChanged(bool value)
    {
        if (value) ApplyTheme(AppTheme.Light);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (value) ApplyTheme(AppTheme.Dark);
    }

    partial void OnIsSystemThemeChanged(bool value)
    {
        if (value) ApplyTheme(AppTheme.Unspecified);
    }

    private void ApplyTheme(AppTheme theme)
    {
        if (_isInitializingTheme) return;
        if (Application.Current is not null)
            Application.Current.UserAppTheme = theme;
        Preferences.Default.Set(AppPreferenceKeys.Theme, theme.ToString());
    }

    private async Task RefreshSignInStateAsync()
    {
        IsSignedIn = await _auth.IsSignedInAsync();
        GoogleButtonText = IsSignedIn ? "Disconnetti account Google" : "Accedi con Google";
    }

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        if (IsBusy) return;

        if (IsSignedIn)
        {
            var confirm = await Shell.Current.DisplayAlertAsync(
                "Disconnetti",
                "Vuoi scollegare il tuo account Google? La sincronizzazione con Drive si disattiverà.",
                "Disconnetti",
                "Annulla");

            if (!confirm) return;

            await _auth.SignOutAsync();
            await RefreshSignInStateAsync();
            return;
        }

        IsBusy = true;
        try
        {
            await _auth.SignInAsync();
            await RefreshSignInStateAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Errore accesso", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsBusy || !IsSignedIn) return;

        IsBusy = true;
        SyncStatus = "Sincronizzazione in corso...";
        try
        {
            await _driveSync.SyncNowAsync();
            SyncStatus = $"Ultima sincronizzazione: {DateTime.Now:dd/MM/yyyy HH:mm}";
        }
        catch (Exception ex)
        {
            SyncStatus = "Sincronizzazione non riuscita.";
            await Shell.Current.DisplayAlertAsync("Errore sincronizzazione", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportHistoricalDataAsync()
    {
        if (IsBusy) return;

        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Seleziona il file JSON dello storico",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } },
                })
            });
        }
        catch (Exception)
        {
            file = null;
        }

        if (file is null) return;

        IsBusy = true;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var count = await _historicalImport.ImportAsync(stream);
            await Shell.Current.DisplayAlertAsync("Import completato", $"Importate {count} transazioni dal file selezionato.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Errore import", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
