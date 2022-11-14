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
            double actualHeight = System.Convert.ToDouble(values[0]);
            double actualWidth = System.Convert.ToDouble(values[1]);
            double wh = 0;
            double res = 1;
            double t = 0;
            try
            {
                double shapeX = System.Convert.ToDouble(values[2]);
                double shapeY = System.Convert.ToDouble(values[3]); 
                
                if (shapeX > shapeY) 
                {
                    res = shapeX;
                    wh = actualWidth;
                }
                else 
                {
                    res = shapeY;
                    wh = actualHeight;
                }
            }
            catch { }
            if (values.Length == 5)
            {
                t = System.Convert.ToDouble(values[4]);
                return t /( wh / (1.4 * res));
            }
            else
            {
                return wh / (1.4 * res);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
