using System.Globalization;

namespace Tirki.Converters;

/// <summary>
/// Colora lo sfondo di una riga transazione: verde pastello per le entrate, rosso/rosa
/// pastello per le uscite. Più alto l'importo (in valore assoluto), più intenso il colore,
/// con una scala a radice quadrata così anche le spese "normali" restano ben distinguibili
/// invece di saturare subito.
/// </summary>
public class AmountBackgroundColorConverter : IValueConverter
{
    private const double ReferenceMax = 400;

    private static readonly Color IncomeLowLight = Color.FromArgb("#F3FBF3");
    private static readonly Color IncomeHighLight = Color.FromArgb("#AEDFB0");
    private static readonly Color ExpenseLowLight = Color.FromArgb("#FDF4F4");
    private static readonly Color ExpenseHighLight = Color.FromArgb("#F0AEB0");

    private static readonly Color IncomeLowDark = Color.FromArgb("#1C231C");
    private static readonly Color IncomeHighDark = Color.FromArgb("#355C38");
    private static readonly Color ExpenseLowDark = Color.FromArgb("#231C1C");
    private static readonly Color ExpenseHighDark = Color.FromArgb("#5C3538");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal amount) return Colors.Transparent;

        var intensity = Math.Min(1.0, Math.Sqrt((double)Math.Abs(amount) / ReferenceMax));
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var (low, high) = (amount >= 0, isDark) switch
        {
            (true, false) => (IncomeLowLight, IncomeHighLight),
            (true, true) => (IncomeLowDark, IncomeHighDark),
            (false, false) => (ExpenseLowLight, ExpenseHighLight),
            (false, true) => (ExpenseLowDark, ExpenseHighDark),
        };

        return Lerp(low, high, intensity);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Color(
            (float)(a.Red + (b.Red - a.Red) * t),
            (float)(a.Green + (b.Green - a.Green) * t),
            (float)(a.Blue + (b.Blue - a.Blue) * t));
    }
}
