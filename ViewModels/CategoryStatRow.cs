namespace Tirki.ViewModels;

/// <summary>Riga di dettaglio nella pagina statistiche: totale di una categoria (o del bucket "Senza categoria") in un intervallo di date.</summary>
public class CategoryStatRow
{
    public required string Name { get; init; }

    public required string ColorHex { get; init; }

    public required decimal Total { get; init; }
}
