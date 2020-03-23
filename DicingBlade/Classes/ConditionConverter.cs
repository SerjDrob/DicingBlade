using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DicingBlade.Classes
{
    class ConditionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            Int32 mask = System.Convert.ToInt32(values[0]);
            int bit=0;
            try { bit = System.Convert.ToInt32(values[1]); }
            catch { }
            return (mask & (1 << bit)) != 0 ? true : false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
