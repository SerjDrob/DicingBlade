using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DicingBlade.Classes
{
    class TransformPointConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double x = 0;
            double y = 0;
            double ActualHeight = 0;
            double ActualWidth = 0;
            double xOffset = 0;
            double yOffset = 0;
            double shapeX = 0;
            double shapeY = 0;
            int selector = System.Convert.ToInt32(parameter);
            TranslateTransform translateTransform1;
            TranslateTransform translateTransform2;
            ScaleTransform scaleTransform;
           

            double wh = 0;
            double res = 1;
            try
            {
                x = System.Convert.ToDouble(values[0]);
                y = System.Convert.ToDouble(values[1]);
                ActualWidth = System.Convert.ToDouble(values[2]);
                ActualHeight = System.Convert.ToDouble(values[3]);
                xOffset = System.Convert.ToDouble(values[4]);
                yOffset = System.Convert.ToDouble(values[5]);
                shapeX = System.Convert.ToDouble(values[6]);
                shapeY = System.Convert.ToDouble(values[7]);
                if (x > y)
                {
                    res = shapeX;
                    wh = ActualWidth;
                }
                else
                {
                    res = shapeY;
                    wh = ActualHeight;
                }
            }
            catch { }

            Point point = new Point(x, y);
            switch (selector)
            {
                case 1:
                    translateTransform1 = new TranslateTransform(xOffset, 0);
                    translateTransform2 = new TranslateTransform(ActualWidth / 2, 0);
                    scaleTransform = new ScaleTransform(wh / (1.4 * res), 1);
                    break;
                case 2:
                    translateTransform1 = new TranslateTransform(0, yOffset);
                    translateTransform2 = new TranslateTransform(0, ActualHeight / 2);
                    scaleTransform = new ScaleTransform(1, wh / (1.4 * res));
                    break;
                default:
                    translateTransform1 = new TranslateTransform(0, 0);
                    translateTransform2 = new TranslateTransform(0, 0);
                    scaleTransform = new ScaleTransform(1, 1);
                    break;
            }
            return translateTransform2.Transform(scaleTransform.Transform(translateTransform1.Transform(point)));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
