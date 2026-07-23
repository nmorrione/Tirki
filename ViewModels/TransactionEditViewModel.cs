using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Models;
using Tirki.Services;

namespace Tirki.ViewModels;

[QueryProperty(nameof(Id), "Id")]
public partial class TransactionEditViewModel : ObservableObject
{
    private readonly LocalDatabaseService _database;
    private readonly AutoSyncService _autoSync;
    private Transaction _transaction = new();

    public TransactionEditViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;
    }

    [ObservableProperty]
    private string? id;

    [ObservableProperty]
    private DateTime date = DateTime.Today;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private bool isIncome;

    [ObservableProperty]
    private bool isExisting;

    [ObservableProperty]
    private string title = "Nuova transazione";

    partial void OnIdChanged(string? value)
    {
        _ = LoadAsync(value);
    }

    private async Task LoadAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var guid))
        {
            _transaction = new Transaction();
            IsExisting = false;
            Title = "Nuova transazione";
            return;
        }

        var existing = await _database.GetTransactionAsync(guid);
        if (existing is null) return;

        _transaction = existing;
        IsExisting = true;
        Title = "Modifica transazione";
        Date = existing.Date;
        Description = existing.Description;
        IsIncome = existing.Amount > 0;
        AmountText = Math.Abs(existing.Amount).ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            await Shell.Current.DisplayAlertAsync("Attenzione", "Inserisci una descrizione.", "OK");
            return;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Attenzione", "Inserisci un importo valido maggiore di zero.", "OK");
            return;
        }

        _transaction.Date = Date;
        _transaction.Description = Description.Trim();
        _transaction.Amount = IsIncome ? amount : -amount;

        await _database.SaveTransactionAsync(_transaction);
        _autoSync.TriggerDebouncedSync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync("Elimina", $"Eliminare \"{Description}\"?", "Elimina", "Annulla");
        if (!confirm) return;

        await _database.DeleteTransactionAsync(_transaction);
        _autoSync.TriggerDebouncedSync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
