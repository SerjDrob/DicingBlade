using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;

namespace DicingBlade.Classes
{
    class ScaleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double height = System.Convert.ToDouble(values[0]);
            double width = System.Convert.ToDouble(values[1]);
            double res = 1;
            try
            {
                double x = System.Convert.ToDouble(values[2]);
                double y = System.Convert.ToDouble(values[3]);
                res = x > y ? x : y;
            }
            catch { }
            if (values.Length == 5)
            {
                double t = System.Convert.ToDouble(values[4]);
                return t /( height / (1.4 * res));
            }
            else
            {
                return height / (1.4 * res);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
