using System;
using System.Globalization;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class RadioEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int bit1, bit2;
            try
            {
                bit1 = System.Convert.ToInt32(value);
                bit2 = System.Convert.ToInt32(parameter);
            }
            catch
            {
                return false;
            }
            return bit1 == bit2;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
