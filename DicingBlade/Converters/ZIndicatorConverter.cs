using System;
using System.Globalization;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class ZIndicatorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var h = System.Convert.ToDouble(values[0]);
                var thickness = System.Convert.ToDouble(values[1]);
                var val = System.Convert.ToDouble(values[2]);
                var ztouch = System.Convert.ToDouble(values[3]);
                var result = Math.Abs((-ztouch + val) * h / 3 * thickness);
                return result > h ? h : result;
            }
            catch { return 0; }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
