using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Services;

namespace Tirki.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly HistoricalImportService _historicalImport;

    public SettingsViewModel(HistoricalImportService historicalImport)
    {
        _historicalImport = historicalImport;

        var savedTheme = Preferences.Default.Get(AppPreferenceKeys.Theme, nameof(AppTheme.Unspecified));
        _isInitializingTheme = true;
        IsLightTheme = savedTheme == nameof(AppTheme.Light);
        IsDarkTheme = savedTheme == nameof(AppTheme.Dark);
        IsSystemTheme = savedTheme == nameof(AppTheme.Unspecified);
        _isInitializingTheme = false;
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

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        await Shell.Current.DisplayAlertAsync(
            "Accedi con Google",
            "L'accesso con Google e la sincronizzazione su Drive arriveranno in una fase successiva.",
            "OK");
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
