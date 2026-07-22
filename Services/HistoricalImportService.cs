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
/// Import di transazioni da un file JSON scelto dall'utente tramite file picker
/// (tipicamente generato a partire dal suo storico Excel). Può essere eseguito
/// più volte: ogni chiamata inserisce le transazioni contenute nel file scelto.
/// </summary>
public class HistoricalImportService
{
    private readonly LocalDatabaseService _database;

    public HistoricalImportService(LocalDatabaseService database)
    {
        _database = database;
    }

    public async Task<int> ImportAsync(Stream jsonStream)
    {
        var items = await JsonSerializer.DeserializeAsync<List<ImportedTransaction>>(jsonStream)
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

        return items.Count;
    }
}
