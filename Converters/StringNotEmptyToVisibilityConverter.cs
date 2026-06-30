using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CafePOS.Converters;

/// <summary>
/// Shows element when string is not null or empty.
/// null/empty → Collapsed, otherwise → Visible.
/// </summary>
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("StringNotEmptyToVisibilityConverter does not support ConvertBack.");
    }
}
