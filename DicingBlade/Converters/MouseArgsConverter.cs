using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using cursor = System.Windows.Forms.Cursor;

namespace DicingBlade.Converters
{
    class MouseArgsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var args = value as MouseButtonEventArgs;
            var field = parameter as System.Windows.Controls.Image;
            var point = args.GetPosition(parameter as IInputElement);
            var newX = cursor.Position.X - point.X + field.ActualWidth/2;
            var newY = cursor.Position.Y - point.Y + field.ActualHeight/2;
            cursor.Position = new System.Drawing.Point((int)newX,(int)newY);
            point = new Point((point.X/field.ActualWidth) - 0.5, (point.Y / field.ActualHeight) - 0.5);            
            return point;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
