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
    class CrossPositionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 1)
            {
                double length = System.Convert.ToDouble(values[0]);
                int pointNum = System.Convert.ToInt32(values[1]);
                double lengthRatio = System.Convert.ToDouble(parameter);
                switch (pointNum)
                {
                    case 1: return length * (1 - lengthRatio) / 2;
                    case 2: return length * (1 + lengthRatio) / 2;
                    default:
                        return 0;
                }
            }
            else
            {
                return System.Convert.ToDouble(values[0]) / 2;
            }

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
