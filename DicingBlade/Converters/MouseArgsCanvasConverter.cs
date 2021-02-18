using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using cursor = System.Windows.Forms.Cursor;

namespace DicingBlade.Converters
{
    class MouseArgsCanvasConverter:IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var args = value as MouseButtonEventArgs;
            var field = parameter as System.Windows.FrameworkElement;
            var point = args.GetPosition(parameter as IInputElement);


            //var point1 = new Point((point.X / field.ActualWidth) - 0.5, (point.Y / field.ActualHeight) - 0.5);
            //var point2 = new Point((point.X / field.ActualHeight) -0.5, (point.Y / field.ActualHeight) - 0.5);
            
            var point1 = new Point((point.X / field.ActualWidth) - 0.5, (point.Y - field.ActualHeight * 0.5) / field.ActualWidth);
            var point2 = new Point((point.X - 0.5 * field.ActualWidth) / field.ActualHeight, (point.Y / field.ActualHeight) - 0.5);
            return new Point[]{ point1, point2};
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
