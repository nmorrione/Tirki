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
    private readonly HistoricalImportService _historicalImport;
    private static readonly CultureInfo ItalianCulture = new("it-IT");

    public TransactionsViewModel(LocalDatabaseService database, HistoricalImportService historicalImport)
    {
        _database = database;
        _historicalImport = historicalImport;
    }

    [ObservableProperty]
    private bool showImportHistoricalData;

    public ObservableCollection<TransactionGroup> Groups { get; } = new();

    [ObservableProperty]
    private decimal balance;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ShowImportHistoricalData = !_historicalImport.HasImported;

            var transactions = await _database.GetTransactionsAsync();
            Balance = transactions.Sum(t => t.Amount);

            var grouped = transactions
                .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
                .OrderByDescending(g => g.Key)
                .Select(g => new TransactionGroup(
                    ItalianCulture.TextInfo.ToTitleCase(g.Key.ToString("MMMM yyyy", ItalianCulture)),
                    g.OrderByDescending(t => t.Date).ToList()));

            Groups.Clear();
            foreach (var group in grouped)
                Groups.Add(group);
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
        IsBusy = true;
        try
        {
            var count = await _historicalImport.ImportAsync();
            ShowImportHistoricalData = !_historicalImport.HasImported;
            await Shell.Current.DisplayAlertAsync("Import completato", $"Importate {count} transazioni storiche.", "OK");
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
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
