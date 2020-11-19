using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DicingBlade.Converters
{
    internal class CursorToPathConverter : IMultiValueConverter
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
            double shift = 0;
            int selector = System.Convert.ToInt32(parameter);
            TranslateTransform translateTransform1X;
            TranslateTransform translateTransform2X;
            TranslateTransform translateTransform1Y;
            TranslateTransform translateTransform2Y;
            ScaleTransform scaleTransformX;
            ScaleTransform scaleTransformY;
            LineGeometry lineGeometryX;
            LineGeometry lineGeometryY;
            GeometryGroup geometryGroup = new GeometryGroup();


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
                if (values.Count() == 9)
                {
                    shift = System.Convert.ToDouble(values[8]);
                }
            }
            catch { }

            translateTransform1X = new TranslateTransform(-xOffset, 0);
            translateTransform2X = new TranslateTransform(actualWidth / 2, 0);
            scaleTransformX = new ScaleTransform(wh / (1.4 * res), 1);

            translateTransform1Y = new TranslateTransform(0, -yOffset);
            translateTransform2Y = new TranslateTransform(0, actualHeight / 2);
            scaleTransformY = new ScaleTransform(1, wh / (1.4 * res));

            Point startPointX = new Point(x, 0);
            Point endPointX = new Point(x, actualHeight);
            Point startPointY = new Point(0, y + shift);
            Point endPointY = new Point(actualWidth, y + shift);


            startPointX = translateTransform2X.Transform(scaleTransformX.Transform(translateTransform1X.Transform(startPointX)));
            startPointY = translateTransform2Y.Transform(scaleTransformY.Transform(translateTransform1Y.Transform(startPointY)));
            endPointX = translateTransform2X.Transform(scaleTransformX.Transform(translateTransform1X.Transform(endPointX)));
            endPointY = translateTransform2Y.Transform(scaleTransformY.Transform(translateTransform1Y.Transform(endPointY)));

            lineGeometryX = new LineGeometry(startPointX, endPointX);
            lineGeometryY = new LineGeometry(startPointY, endPointY);
            geometryGroup.Children.Add(lineGeometryX);
            geometryGroup.Children.Add(lineGeometryY);


            return geometryGroup;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
