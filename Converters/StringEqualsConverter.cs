using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AttandenceDesktop.Converters
{
    public class StringEqualsConverter : IValueConverter, IMultiValueConverter
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
        
        // Support for MultiBinding to handle status-based formatting
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // This method is for handling multiple bindings to determine output value
            if (values.Count < 3 || values[0] == null)
                return "Black";

            string mode = parameter?.ToString() ?? "Status";
            string value = values[0]?.ToString() ?? "";
                
            // Loop through the values to check matches and return corresponding values
            for (int i = 1; i < values.Count; i += 2)
            {
                if (i + 1 < values.Count && 
                    values[i] != null && 
                    value.Equals(values[i].ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return values[i + 1]; // Return the matching value
                }
            }
            
            // Default value is the last item if even number of items, otherwise "Black" or "Normal"
            return values.Count % 2 == 0 ? values[values.Count - 1] : (mode == "Weight" ? "Normal" : "Black");
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