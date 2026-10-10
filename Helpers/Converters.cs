using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Uttu.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

public class OverLimitToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(160, 160, 160));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(216, 59, 1)); // Windows Red

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOverLimit && isOverLimit)
        {
            return WarningBrush;
        }
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PlatformToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush XBrush = new(Color.FromRgb(29, 155, 240));
    private static readonly SolidColorBrush LinkedInBrush = new(Color.FromRgb(10, 102, 194));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0, 120, 215));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string plat = value?.ToString() ?? "";
        if (plat.Contains("X", StringComparison.OrdinalIgnoreCase) || plat.Contains("Twitter", StringComparison.OrdinalIgnoreCase))
            return XBrush;
        if (plat.Contains("LinkedIn", StringComparison.OrdinalIgnoreCase))
            return LinkedInBrush;

        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
