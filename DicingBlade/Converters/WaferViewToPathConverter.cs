using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using netDxf.Entities;
using System.Collections.ObjectModel;

namespace DicingBlade.Converters
{
    internal class WaferViewToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var waferView = new ObservableCollection<Line>();
            try
            {
                waferView = (ObservableCollection<Line>)value;
            }
            catch { }

            var geometryGroup = new GeometryGroup();
            foreach (var line in waferView)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new System.Windows.Point(line.StartPoint.X, line.StartPoint.Y),
                    new System.Windows.Point(line.EndPoint.X, line.EndPoint.Y)
                    ));
            }

            return geometryGroup;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
