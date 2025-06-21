using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AttandenceDesktop.Converters
{
    public class StringEqualsConverter : IValueConverter
    {
        public static readonly StringEqualsConverter Instance = new StringEqualsConverter();
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return false;
            }
            
            return value.ToString()!.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 