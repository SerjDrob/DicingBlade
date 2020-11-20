using System;
using System.Globalization;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class TraceAngleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {

            try
            {
                // var rotation = System.Convert.ToBoolean(values[2]);
                // return rotation?System.Convert.ToDouble(values[0]) - System.Convert.ToDouble(values[1]): System.Convert.ToDouble(values[0]);
                return System.Convert.ToDouble(values[0]) - System.Convert.ToDouble(values[1]);
            }
            catch { }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
