using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CafePOS.Converters;

/// <summary>
/// Converts a boolean value to a Visibility value.
/// true → Visible, false → Collapsed.
/// Pass "Invert" as parameter to reverse the logic.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);

        if (invert) boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isVisible = value is Visibility.Visible;
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);

        return invert ? !isVisible : isVisible;
    }
}
