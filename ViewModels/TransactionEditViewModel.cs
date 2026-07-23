using System.Collections.ObjectModel;
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
    private DateTime? _originalDate;

    public TransactionEditViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;
        _ = LoadPresetsAsync();
    }

    public ObservableCollection<TransactionPreset> Presets { get; } = new();

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
            _originalDate = null;
            IsExisting = false;
            Title = "Nuova transazione";
            return;
        }

        var existing = await _database.GetTransactionAsync(guid);
        if (existing is null) return;

        _transaction = existing;
        _originalDate = existing.Date;
        IsExisting = true;
        Title = "Modifica transazione";
        Date = existing.Date;
        Description = existing.Description;
        IsIncome = existing.Amount > 0;
        AmountText = Math.Abs(existing.Amount).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<decimal?> ValidateAndParseAmountAsync()
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            await Shell.Current.DisplayAlertAsync("Attenzione", "Inserisci una descrizione.", "OK");
            return null;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Attenzione", "Inserisci un importo valido maggiore di zero.", "OK");
            return null;
        }

        return amount;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var amount = await ValidateAndParseAmountAsync();
        if (amount is null) return;

        _transaction.Date = Date;
        _transaction.Description = Description.Trim();
        _transaction.Amount = IsIncome ? amount.Value : -amount.Value;

        await _database.SaveTransactionAsync(_transaction);
        _autoSync.MarkTransactionDirty(_transaction, _originalDate);
        _autoSync.TriggerDebouncedSync();
        await Shell.Current.GoToAsync("..");
    }

    private async Task LoadPresetsAsync()
    {
        var presets = await _database.GetPresetsAsync();
        Presets.Clear();
        foreach (var preset in presets)
            Presets.Add(preset);
    }

    [RelayCommand]
    private void ApplyPreset(TransactionPreset preset)
    {
        Description = preset.Description;
        AmountText = Math.Abs(preset.Amount).ToString(CultureInfo.InvariantCulture);
        IsIncome = preset.Amount > 0;
    }

    [RelayCommand]
    private async Task SaveAsPresetAsync()
    {
        var amount = await ValidateAndParseAmountAsync();
        if (amount is null) return;

        var preset = new TransactionPreset
        {
            Description = Description.Trim(),
            Amount = IsIncome ? amount.Value : -amount.Value,
        };

        await _database.SavePresetAsync(preset);
        _autoSync.TriggerDebouncedSync();
        Presets.Add(preset);
    }

    [RelayCommand]
    private async Task DeletePresetAsync(TransactionPreset preset)
    {
        var confirm = await Shell.Current.DisplayAlertAsync("Elimina preset", $"Eliminare il preset \"{preset.Description}\"?", "Elimina", "Annulla");
        if (!confirm) return;

        await _database.DeletePresetAsync(preset);
        _autoSync.TriggerDebouncedSync();
        Presets.Remove(preset);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync("Elimina", $"Eliminare \"{Description}\"?", "Elimina", "Annulla");
        if (!confirm) return;

        await _database.DeleteTransactionAsync(_transaction);
        _autoSync.MarkTransactionDirty(_transaction);
        _autoSync.TriggerDebouncedSync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
