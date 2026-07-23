namespace Tirki.Models;

/// <summary>
/// Piccolo indice su Drive: per ogni mese ("yyyy-MM") con transazioni, l'UpdatedAt più recente
/// che quel mese conteneva all'ultimo upload. Permette a un device di scoprire con un solo
/// download quali mesi sono cambiati altrove, senza dover riscaricare tutto lo storico.
/// </summary>
public class SyncManifest
{
    public Dictionary<string, DateTime> MonthWatermarks { get; set; } = new();
}
