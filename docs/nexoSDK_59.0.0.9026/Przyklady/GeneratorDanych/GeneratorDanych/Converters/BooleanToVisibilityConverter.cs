using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GeneratorDanych.Converters
{
	public class BooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool boolean)
			{
				return boolean ? Visibility.Visible : GetVisibilityWhenNotTrue(parameter);
			}
			return GetVisibilityWhenNotTrue(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Visibility visibility)
			{
				return visibility == Visibility.Visible;
			}
			return false;
		}

		private static Visibility GetVisibilityWhenNotTrue(object parameter)
		{
			if (parameter != null && Enum.IsDefined(typeof(Visibility), parameter))
			{
				return (Visibility)Enum.Parse(typeof(Visibility), parameter.ToString());
			}
			return Visibility.Collapsed;
		}
	}
}