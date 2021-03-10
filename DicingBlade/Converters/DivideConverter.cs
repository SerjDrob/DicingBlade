using System;
using System.Globalization;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    class DivideConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = new double();
            var par = new double();
            try
            {
                val = System.Convert.ToDouble(value);
                par = System.Convert.ToDouble(parameter);
            }
            catch (Exception)
            {
                throw;
            }
            return val / par;
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
