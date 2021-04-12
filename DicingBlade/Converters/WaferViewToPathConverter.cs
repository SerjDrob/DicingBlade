using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using netDxf.Entities;
using System.Collections.ObjectModel;
using DicingBlade.Classes;

namespace DicingBlade.Converters
{
    internal class WaferViewToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var waferView = new ObservableCollection<Line2D>();
            try
            {
                waferView = (ObservableCollection<Line2D>)value;
            }
            catch { }

            var geometryGroup = new GeometryGroup();
            foreach (var line in waferView)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new System.Windows.Point(line.Start.X, line.Start.Y),
                    new System.Windows.Point(line.End.X, line.End.Y)
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
