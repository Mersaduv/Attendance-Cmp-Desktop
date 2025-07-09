using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AttandenceDesktop.Converters
{
    public class StatusToBgConverter : IValueConverter
    {
        public static readonly StatusToBgConverter Instance = new StatusToBgConverter();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string status)
                return new SolidColorBrush(Colors.Transparent);
                
            return status switch
            {
                "Late" => new SolidColorBrush(Color.Parse("#FFEBEE")),
                "Left Early" => new SolidColorBrush(Color.Parse("#FFF8E1")),
                "Late & Left Early" => new SolidColorBrush(Color.Parse("#FFEBEE")),
                "Overtime" => new SolidColorBrush(Color.Parse("#E8F5E9")),
                "Early Arrival" => new SolidColorBrush(Color.Parse("#E3F2FD")),
                "Half Day" => new SolidColorBrush(Color.Parse("#F9FBE7")),
                "Complete" => new SolidColorBrush(Color.Parse("#E8F5E9")),
                "Checked In" => new SolidColorBrush(Color.Parse("#E0F7FA")),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }
        
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class StatusToFgConverter : IValueConverter
    {
        public static readonly StatusToFgConverter Instance = new StatusToFgConverter();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string status)
                return new SolidColorBrush(Colors.Black);
                
            return status switch
            {
                "Late" => new SolidColorBrush(Color.Parse("#D32F2F")),
                "Left Early" => new SolidColorBrush(Color.Parse("#FF9800")),
                "Late & Left Early" => new SolidColorBrush(Color.Parse("#D32F2F")),
                "Overtime" => new SolidColorBrush(Color.Parse("#2E7D32")),
                "Early Arrival" => new SolidColorBrush(Color.Parse("#1976D2")),
                "Half Day" => new SolidColorBrush(Color.Parse("#827717")),
                "Complete" => new SolidColorBrush(Color.Parse("#2E7D32")),
                "Checked In" => new SolidColorBrush(Color.Parse("#00838F")),
                _ => new SolidColorBrush(Color.Parse("#616161"))
            };
        }
        
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                if (string.IsNullOrWhiteSpace(status))
                {
                    return new SolidColorBrush(Colors.Gray);
                }
                
                if (status.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || 
                    status.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
                    status.Contains("failed") || 
                    status.Contains("error"))
                {
                    return new SolidColorBrush(Colors.Red);
                }
                
                if (status.StartsWith("Sync completed", StringComparison.OrdinalIgnoreCase) || 
                    status.Contains("successfully"))
                {
                    return new SolidColorBrush(Colors.Green);
                }
                
                if (status.StartsWith("Connecting", StringComparison.OrdinalIgnoreCase) || 
                    status.StartsWith("Syncing", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Colors.Blue);
                }
            }
            
            return new SolidColorBrush(Colors.Gray);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 