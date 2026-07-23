namespace Tirki.Models;

/// <summary>Struttura del file JSON sincronizzato su Google Drive.</summary>
public class SyncPayload
{
    public List<Transaction> Transactions { get; set; } = new();

    public List<Category> Categories { get; set; } = new();
}
