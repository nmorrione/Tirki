using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Models;
using Tirki.Services;

namespace Tirki.ViewModels;

public partial class TransactionsViewModel : ObservableObject
{
    private readonly LocalDatabaseService _database;
    private static readonly CultureInfo ItalianCulture = new("it-IT");
    private bool _suppressFilterReload;

    public TransactionsViewModel(LocalDatabaseService database)
    {
        _database = database;

        var today = DateTime.Today;
        _suppressFilterReload = true;
        FilterFrom = new DateTime(today.Year, today.Month, 1);
        FilterTo = FilterFrom.AddMonths(1).AddDays(-1);
        _suppressFilterReload = false;
    }

    public ObservableCollection<TransactionGroup> Groups { get; } = new();

    [ObservableProperty]
    private decimal balance;

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
            Balance = await _database.GetBalanceAsync();

            var from = FilterFrom.Date;
            var to = FilterTo.Date.AddDays(1).AddTicks(-1);
            var transactions = await _database.GetTransactionsAsync(from, to);

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
        await LoadAsync();
    }
}
