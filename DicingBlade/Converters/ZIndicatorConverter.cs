using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    class ZIndicatorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double H = System.Convert.ToDouble(values[0]);
            double thickness = System.Convert.ToDouble(values[1]);
            double val = System.Convert.ToDouble(values[2]);
            return Math.Abs(val * 3 * H/thickness)>H?H:Math.Abs(val * 3 * H / thickness);

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
