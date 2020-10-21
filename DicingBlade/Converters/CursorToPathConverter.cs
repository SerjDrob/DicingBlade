using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DicingBlade.Classes
{
    class CursorToPathConverter : IMultiValueConverter
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
            TranslateTransform translateTransform1x;
            TranslateTransform translateTransform2x;
            TranslateTransform translateTransform1y;
            TranslateTransform translateTransform2y;
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
            }
            catch { }

            translateTransform1x = new TranslateTransform(-xOffset, 0);
            translateTransform2x = new TranslateTransform(actualWidth / 2, 0);
            scaleTransformX = new ScaleTransform(wh / (1.4 * res), 1);

            translateTransform1y = new TranslateTransform(0, -yOffset);
            translateTransform2y = new TranslateTransform(0, actualHeight / 2);
            scaleTransformY = new ScaleTransform(1, wh / (1.4 * res));

            Point StartPointX = new Point(x, 0);
            Point EndPointX = new Point(x,actualHeight);
            Point StartPointY = new Point(0, y);
            Point EndPointY = new Point(actualWidth,y);


            StartPointX = translateTransform2x.Transform(scaleTransformX.Transform(translateTransform1x.Transform(StartPointX)));
            StartPointY = translateTransform2y.Transform(scaleTransformY.Transform(translateTransform1y.Transform(StartPointY)));
            EndPointX = translateTransform2x.Transform(scaleTransformX.Transform(translateTransform1x.Transform(EndPointX)));
            EndPointY = translateTransform2y.Transform(scaleTransformY.Transform(translateTransform1y.Transform(EndPointY)));

            lineGeometryX = new LineGeometry(StartPointX, EndPointX);
            lineGeometryY = new LineGeometry(StartPointY, EndPointY);
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
