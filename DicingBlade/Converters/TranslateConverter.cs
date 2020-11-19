using System;
using System.Globalization;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class TranslateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double offset = 0;
            try
            {
                offset = System.Convert.ToDouble(values[0]) / 2;
            }
            catch { }
            return offset;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
