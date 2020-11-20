using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class DoubleToDurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int sec = 1;
            try
            {
                sec = System.Convert.ToInt32(value);
            }
            catch { }
            return new Duration(new TimeSpan(0, 0, sec));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
