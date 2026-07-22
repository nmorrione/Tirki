using System.Text.Json;
using System.Text.Json.Serialization;
using Tirki.Models;

namespace Tirki.Services;

public class ImportedTransaction
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

/// <summary>
/// Import una-tantum dello storico 2024-2025 proveniente da NIKO_SPESE.xlsx,
/// bundlato come Resources/Raw/storico_import.json. Idempotente: controlla
/// un flag in Preferences per non duplicare le transazioni se rieseguito.
/// </summary>
public class HistoricalImportService
{
    private const string ImportedFlagKey = "historical_import_done";
    private const string AssetFileName = "storico_import.json";

    private readonly LocalDatabaseService _database;

    public HistoricalImportService(LocalDatabaseService database)
    {
        _database = database;
    }

    public bool HasImported => Preferences.Default.Get(ImportedFlagKey, false);

    public async Task<int> ImportAsync()
    {
        if (HasImported) return 0;

        using var stream = await FileSystem.OpenAppPackageFileAsync(AssetFileName);
        var items = await JsonSerializer.DeserializeAsync<List<ImportedTransaction>>(stream)
            ?? new List<ImportedTransaction>();

        foreach (var item in items)
        {
            var transaction = new Transaction
            {
                Date = item.Date,
                Description = item.Description,
                Amount = item.Amount
            };
            await _database.SaveTransactionAsync(transaction);
        }

        Preferences.Default.Set(ImportedFlagKey, true);
        return items.Count;
    }
}
