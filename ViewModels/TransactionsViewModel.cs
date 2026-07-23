using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Models;
using Tirki.Services;

namespace Tirki.ViewModels;

public partial class TransactionsViewModel : ObservableObject
{
    private const string BalanceHiddenKey = "balance_hidden";

    private readonly LocalDatabaseService _database;
    private readonly AutoSyncService _autoSync;
    private static readonly CultureInfo ItalianCulture = new("it-IT");
    private bool _suppressFilterReload;
    private string? _lastLoadedSignature;

    public TransactionsViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;

        var today = DateTime.Today;
        _suppressFilterReload = true;
        FilterFrom = new DateTime(today.Year, today.Month, 1);
        FilterTo = FilterFrom.AddMonths(1).AddDays(-1);
        _suppressFilterReload = false;

        isBalanceHidden = Preferences.Default.Get(BalanceHiddenKey, false);

        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, _) => OnPropertyChanged(nameof(EyeIconSource));

        // Il sync periodico in background può portare modifiche fatte da un altro device: qui la
        // griglia si aggiorna in silenzio (LoadAsync salta il ridisegno se in realtà nulla è cambiato).
        _autoSync.SyncCompleted += () => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<TransactionGroup> Groups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayBalance))]
    private decimal balance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayBalance))]
    [NotifyPropertyChangedFor(nameof(EyeIconSource))]
    private bool isBalanceHidden;

    public string DisplayBalance => IsBalanceHidden ? "•••••• €" : Balance.ToString("C", ItalianCulture);

    public string EyeIconSource
    {
        get
        {
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            return (IsBalanceHidden, isDark) switch
            {
                (false, false) => "icon_eye.svg",
                (false, true) => "icon_eye_dark.svg",
                (true, false) => "icon_eye_off.svg",
                (true, true) => "icon_eye_off_dark.svg",
            };
        }
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private DateTime filterFrom;

    [ObservableProperty]
    private DateTime filterTo;

    [ObservableProperty]
    private string currentSectionName = string.Empty;

    [ObservableProperty]
    private decimal currentSectionTotal;

    [ObservableProperty]
    private bool isCurrentSectionVisible;

    partial void OnFilterFromChanged(DateTime value) => ReloadFromFilterChange();

    partial void OnFilterToChanged(DateTime value) => ReloadFromFilterChange();

    private void ReloadFromFilterChange()
    {
        if (_suppressFilterReload) return;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task SetCurrentMonthAsync()
    {
        var today = DateTime.Today;
        _suppressFilterReload = true;
        FilterFrom = new DateTime(today.Year, today.Month, 1);
        FilterTo = FilterFrom.AddMonths(1).AddDays(-1);
        _suppressFilterReload = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var newBalance = await _database.GetBalanceAsync();

            var from = FilterFrom.Date;
            var to = FilterTo.Date.AddDays(1).AddTicks(-1);
            var transactions = await _database.GetTransactionsAsync(from, to);

            // Un sync in background trova spesso nulla di nuovo per la vista corrente: evitare di
            // ridisegnare la griglia in quei casi risparmia all'utente uno scroll-reset non necessario.
            var signature = BuildSignature(newBalance, transactions);
            if (signature == _lastLoadedSignature) return;
            _lastLoadedSignature = signature;

            Balance = newBalance;

            var grouped = transactions
                .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
                .OrderByDescending(g => g.Key)
                .Select(g => new TransactionGroup(
                    ItalianCulture.TextInfo.ToTitleCase(g.Key.ToString("MMMM yyyy", ItalianCulture)),
                    g.OrderByDescending(t => t.Date).ToList()));

            Groups.Clear();
            foreach (var group in grouped)
                Groups.Add(group);

            if (Groups.Count > 0)
            {
                CurrentSectionName = Groups[0].Name;
                CurrentSectionTotal = Groups[0].Total;
                // L'header vero del primo gruppo è ancora visibile in cima: niente barra sticky finché non si scrolla oltre.
                IsCurrentSectionVisible = false;
            }
            else
            {
                IsCurrentSectionVisible = false;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildSignature(decimal balance, List<Transaction> transactions)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(balance).Append('|');
        foreach (var t in transactions)
            sb.Append(t.Id).Append(':').Append(t.Amount).Append(':').Append(t.Description).Append(':').Append(t.Date.Ticks).Append(';');
        return sb.ToString();
    }

    /// <summary>
    /// Chiamato dal code-behind quando lo scroll della lista cambia il primo elemento visibile.
    /// L'indice è "piatto": conta sia gli header di gruppo sia le transazioni.
    /// </summary>
    public void UpdateCurrentSection(int flatFirstVisibleIndex)
    {
        if (Groups.Count == 0) return;

        var target = Groups[^1];
        var targetHeaderIndex = 0;
        var cursor = 0;
        foreach (var group in Groups)
        {
            var groupSpan = 1 + group.Count; // 1 per l'header del gruppo
            if (flatFirstVisibleIndex < cursor + groupSpan)
            {
                target = group;
                targetHeaderIndex = cursor;
                break;
            }
            cursor += groupSpan;
        }

        CurrentSectionName = target.Name;
        CurrentSectionTotal = target.Total;

        // La barra sticky serve solo a sostituire l'header vero quando è già scomparso scrollando:
        // se siamo ancora fermi sull'header stesso (flatFirstVisibleIndex == targetHeaderIndex), nasconderla evita di duplicare il nome del mese.
        IsCurrentSectionVisible = flatFirstVisibleIndex > targetHeaderIndex;
    }

    [RelayCommand]
    private void ToggleBalanceVisibility()
    {
        IsBalanceHidden = !IsBalanceHidden;
        Preferences.Default.Set(BalanceHiddenKey, IsBalanceHidden);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.SettingsPage));
    }

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.TransactionEditPage));
    }

    [RelayCommand]
    private async Task EditTransactionAsync(Transaction transaction)
    {
        await Shell.Current.GoToAsync(nameof(Views.TransactionEditPage), new Dictionary<string, object>
        {
            ["Id"] = transaction.Id.ToString()
        });
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction transaction)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Elimina",
            $"Eliminare \"{transaction.Description}\"?",
            "Elimina",
            "Annulla");

        if (!confirm) return;

        await _database.DeleteTransactionAsync(transaction);
        _autoSync.MarkTransactionDirty(transaction);
        _autoSync.TriggerDebouncedSync();
        await LoadAsync();
    }
}
