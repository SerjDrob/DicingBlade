using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using DicingBlade.Classes;
using System.Windows.Media;
using System.Collections;
using netDxf.Entities;
using System.Collections.ObjectModel;

namespace DicingBlade.Converters
{
    class WaferViewToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ObservableCollection<Line> waferView = new ObservableCollection<Line>();
            try
            {                
                waferView = (ObservableCollection<Line>)value;
            }
            catch { }
            
            GeometryGroup geometryGroup = new GeometryGroup();
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
