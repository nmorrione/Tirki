using System.Globalization;
using Tirki.Models;

namespace Tirki.Converters;

/// <summary>
/// Confronta la categoria di una chip con quella attualmente selezionata nel form transazione,
/// per attenuare visivamente (opacità ridotta) le chip non selezionate.
/// </summary>
public class CategorySelectedConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [Category current, Category selected] && current.Id == selected.Id)
            return 1.0;

        return 0.55;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
