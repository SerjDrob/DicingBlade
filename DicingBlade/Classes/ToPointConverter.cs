using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace DicingBlade.Classes
{
    class ToPointConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {            
            double x = 0;
            double y = 0;
            try
            {
                x = System.Convert.ToDouble(values[0]);
                y = System.Convert.ToDouble(values[1]);                
            }
            catch
            {                
            }
            return new System.Windows.Point(x, y);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
