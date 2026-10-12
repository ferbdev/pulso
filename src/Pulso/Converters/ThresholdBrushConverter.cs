using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Pulso.Converters;

/// <summary>
/// Converte um valor numérico em cor por faixa. ConverterParameter = "aviso,crítico" (ex.: "60,80").
/// Valor nulo vira cinza (sensor indisponível).
/// </summary>
public sealed class ThresholdBrushConverter : IValueConverter
{
    public static readonly Brush Normal = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)));
    public static readonly Brush Warning = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xCC, 0x15)));
    public static readonly Brush Critical = Freeze(new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)));
    public static readonly Brush Unavailable = Freeze(new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double number)
        {
            return Unavailable;
        }

        var (warning, critical) = ParseThresholds(parameter as string);
        if (number >= critical)
        {
            return Critical;
        }

        return number >= warning ? Warning : Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static (double Warning, double Critical) ParseThresholds(string? parameter)
    {
        var parts = parameter?.Split(',');
        if (parts is { Length: 2 }
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var warning)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var critical))
        {
            return (warning, critical);
        }

        return (60, 80);
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
