using System.Globalization;

namespace Tirki.Converters;

/// <summary>
/// Confronta due stringhe (es. il colore di uno swatch con quello attualmente selezionato)
/// e restituisce uno spessore di bordo diverso per evidenziare quello selezionato.
/// </summary>
public class StringEqualsConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [string a, string b] && string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return 4.0;

        return 0.0;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
