using System;
using System.Globalization;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class ConditionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            Int32 mask = 0;
            int bit = 0;
            try
            {
                mask = System.Convert.ToInt32(values[0]);
                bit = System.Convert.ToInt32(values[1]);
            }
            catch { }
            return (mask & (1 << bit)) != 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
