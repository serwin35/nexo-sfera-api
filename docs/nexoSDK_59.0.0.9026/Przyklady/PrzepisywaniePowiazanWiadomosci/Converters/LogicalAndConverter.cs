using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace PrzepisywaniePowiazanWiadomosci.Converters
{
    public class LogicalAndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValues = values.OfType<bool>();
            bool result = boolValues.Contains(false);
            return !result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
