using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

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
    
    public class AlertKindToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string alertKind)
                return Brushes.Black;
                
            return alertKind switch
            {
                "Success" => new SolidColorBrush(Colors.Green),
                "Warning" => new SolidColorBrush(Colors.Orange),
                "Error" => new SolidColorBrush(Colors.Red),
                "Info" => new SolidColorBrush(Colors.Blue),
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 