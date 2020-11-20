using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DicingBlade.Converters
{
    internal class TransformPointConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double x = 0;
            double y = 0;
            double actualHeight = 0;
            double actualWidth = 0;
            double xOffset = 0;
            double yOffset = 0;
            double shapeX = 0;
            double shapeY = 0;
            int selector = System.Convert.ToInt32(parameter);
            TranslateTransform translateTransform1;
            TranslateTransform translateTransform2;
            ScaleTransform scaleTransform;
            LineGeometry lineGeometry = new LineGeometry();

            double wh = 0;
            double res = 1;
            try
            {
                x = System.Convert.ToDouble(values[0]);
                y = System.Convert.ToDouble(values[1]);
                actualWidth = System.Convert.ToDouble(values[2]);
                actualHeight = System.Convert.ToDouble(values[3]);
                xOffset = System.Convert.ToDouble(values[4]);
                yOffset = System.Convert.ToDouble(values[5]);
                shapeX = System.Convert.ToDouble(values[6]);
                shapeY = System.Convert.ToDouble(values[7]);
                if (x > y)
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

            //Point point = new Point(x, y);
            switch (selector)
            {
                case 1:
                    translateTransform1 = new TranslateTransform(xOffset, 0);
                    translateTransform2 = new TranslateTransform(actualWidth / 2, 0);
                    scaleTransform = new ScaleTransform(wh / (1.4 * res), 1);
                    break;
                case 2:
                    translateTransform1 = new TranslateTransform(0, yOffset);
                    translateTransform2 = new TranslateTransform(0, actualHeight / 2);
                    scaleTransform = new ScaleTransform(1, wh / (1.4 * res));
                    break;
                default:
                    translateTransform1 = new TranslateTransform(0, 0);
                    translateTransform2 = new TranslateTransform(0, 0);
                    scaleTransform = new ScaleTransform(1, 1);
                    break;
            }
            return translateTransform2.Transform(scaleTransform.Transform(translateTransform1.Transform(new Point(x, y))));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
