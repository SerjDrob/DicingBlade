using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;

namespace DicingBlade.Converters
{
    internal class TraceConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double x = 0;
            double y = 0;
            double x1 = 0;
            double actualHeight = 0;
            double actualWidth = 0;
            double xOffset = 0;
            double yOffset = 0;
            double shapeX = 0;
            double shapeY = 0;
            double shift = 0;
            int selector = System.Convert.ToInt32(parameter);
            TranslateTransform translateTransform1;
            TranslateTransform translateTransform2;
            ScaleTransform scaleTransform;
            LineGeometry lineGeometry;
            GeometryGroup geometryGroup = new GeometryGroup();


            double wh = 0;
            double res = 1;
            try
            {
                x = System.Convert.ToDouble(values[0]);
                y = System.Convert.ToDouble(values[1]);
                x1 = System.Convert.ToDouble(values[2]);
                actualWidth = System.Convert.ToDouble(values[3]);
                actualHeight = System.Convert.ToDouble(values[4]);
                xOffset = System.Convert.ToDouble(values[5]);
                yOffset = System.Convert.ToDouble(values[6]);
                shapeX = System.Convert.ToDouble(values[7]);
                shapeY = System.Convert.ToDouble(values[8]);
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
                shift = System.Convert.ToDouble(values[9]);
            }
            catch
            {
            }

            translateTransform1 = new TranslateTransform(-xOffset, -yOffset);
            translateTransform2 = new TranslateTransform(actualWidth / 2, actualHeight / 2);
            scaleTransform = new ScaleTransform(wh / (1.4 * res), wh / (1.4 * res));

            Point startPoint = new Point(x, y + shift);
            Point endPoint = new Point(x1, y + shift);

            startPoint = translateTransform2.Transform(scaleTransform.Transform(translateTransform1.Transform(startPoint)));
            endPoint = translateTransform2.Transform(scaleTransform.Transform(translateTransform1.Transform(endPoint)));


            lineGeometry = new LineGeometry(startPoint, endPoint);
            geometryGroup.Children.Add(lineGeometry);

            return geometryGroup;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
