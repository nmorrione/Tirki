using System.Globalization;

namespace Tirki.Converters;

public class CurrencyConverter : IValueConverter
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal d ? d.ToString("C", Italian) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
