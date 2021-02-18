
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DicingBlade.Converters
{
    internal class ScaleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var actualHeight = System.Convert.ToDouble(values[0]);
            var actualWidth = System.Convert.ToDouble(values[1]);
            var wh = 0.0;
            var res = 1.0;
            try
            {
                var shapeX = System.Convert.ToDouble(values[2]);
                var shapeY = System.Convert.ToDouble(values[3]);

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
            catch
            {
                // WTF
            }

            var tmp = wh / (1.4 * res);

            if (values.Length == 5)
            {
                var value = values[4];
                if (value != DependencyProperty.UnsetValue && value is IConvertible convertible)
                {
                    var t = System.Convert.ToDouble(convertible);
                    return t / tmp;
                }
            }

            return tmp;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
