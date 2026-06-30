using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CafePOS.Converters;

/// <summary>
/// Converts bool to "إلغاء الخصم" (true) or "تطبيق الخصم" (false).
/// </summary>
public class BoolToDiscountTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "إلغاء الخصم" : "تطبيق الخصم";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
