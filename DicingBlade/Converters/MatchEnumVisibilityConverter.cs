﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace DicingBlade.Converters
{
    class MatchEnumVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int bit1, bit2;
            try
            {
                bit1 = System.Convert.ToInt32(value);
                bit2 = System.Convert.ToInt32(parameter);
            }
            catch 
            {
                return Visibility.Hidden;
            }
            return bit1 == bit2 ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
