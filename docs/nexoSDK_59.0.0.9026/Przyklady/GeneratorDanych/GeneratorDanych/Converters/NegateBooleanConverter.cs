using System;
using System.Globalization;
using System.Windows.Data;

namespace GeneratorDanych.Converters
{
	public class NegateBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return NegateValue(value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return NegateValue(value);
		}

		private static object NegateValue(object value)
		{
			if (value is bool boolean)
			{
				return !boolean;
			}
			return null;
		}
	}
}