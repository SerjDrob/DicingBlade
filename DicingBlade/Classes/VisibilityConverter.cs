using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace DicingBlade.Classes
{
    class VisibilityConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((string)parameter )
            {
                case "round": 
                    {
                        return (bool)value ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    }
                case "square": 
                    {
                        return (bool)value ? Visibility.Collapsed : Visibility.Visible;
                        break;
                    }
                default:
                    return (bool)value ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }            
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
