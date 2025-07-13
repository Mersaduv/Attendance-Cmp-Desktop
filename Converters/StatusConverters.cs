using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Converter for attendance status that returns appropriate color and styling
    /// </summary>
    public class AttendanceStatusConverter : IValueConverter 
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string status)
                return new SolidColorBrush(Colors.Black);
            
            // If parameter is "FontWeight", return appropriate font weight
            if (parameter is string param && param == "FontWeight")
            {
                return status switch
                {
                    "Absent" => FontWeight.Bold,
                    "Leave" => FontWeight.SemiBold,
                    _ => FontWeight.Normal
                };
            }
            
            // Default behavior: return appropriate color brush
            return status switch
            {
                "Absent" => new SolidColorBrush(Color.Parse("#D32F2F")), // Red
                "Leave" => new SolidColorBrush(Color.Parse("#FFA000")),  // Orange/Amber
                "Late Arrival" => new SolidColorBrush(Color.Parse("#D81B60")), // Pink
                "Early Departure" => new SolidColorBrush(Color.Parse("#FF9800")), // Orange
                "Late & Left Early" => new SolidColorBrush(Color.Parse("#D32F2F")), // Red
                "Overtime" => new SolidColorBrush(Color.Parse("#2E7D32")), // Green
                "Early Arrival" => new SolidColorBrush(Color.Parse("#1976D2")), // Blue
                "Half Day" => new SolidColorBrush(Color.Parse("#827717")), // Olive/Yellow
                "Present" => new SolidColorBrush(Color.Parse("#000000")), // Black
                "Holiday" => new SolidColorBrush(Color.Parse("#7B1FA2")), // Purple
                "Non-Working Day" => new SolidColorBrush(Color.Parse("#5D4037")), // Brown
                "Scheduled" => new SolidColorBrush(Color.Parse("#607D8B")), // Blue Grey
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that checks if a collection contains a specific item
    /// </summary>
    public class CollectionContainsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;
                
            if (value is IEnumerable<int> collection && parameter is int item)
            {
                return collection.Contains(item);
            }
            
            if (value is IEnumerable<string> stringCollection && parameter is string stringItem)
            {
                return stringCollection.Contains(stringItem);
            }
            
            if (value is IList list)
            {
                return list.Contains(parameter);
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 