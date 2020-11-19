using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class VisibilityConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (string) parameter switch
            {
                "round" => (bool) value ? Visibility.Visible : Visibility.Collapsed,
                "square" => (bool) value ? Visibility.Collapsed : Visibility.Visible,
                _ => (bool) value ? Visibility.Visible : Visibility.Collapsed,
            };
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
