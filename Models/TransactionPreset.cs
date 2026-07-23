using SQLite;

namespace Tirki.Models;

/// <summary>Modello riutilizzabile per inserire al volo una spesa/entrata ricorrente.</summary>
public class TransactionPreset
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Description { get; set; } = string.Empty;

    /// <summary>Segno indica entrata (positivo) o uscita (negativo), come in Transaction.</summary>
    public decimal Amount { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
}
