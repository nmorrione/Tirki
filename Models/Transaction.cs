using SQLite;

namespace Tirki.Models;

public class Transaction
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed]
    public DateTime Date { get; set; } = DateTime.Today;

    public string Description { get; set; } = string.Empty;

    /// <summary>Segno indica entrata (positivo) o uscita (negativo).</summary>
    public decimal Amount { get; set; }

    public Guid? CategoryId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
}
