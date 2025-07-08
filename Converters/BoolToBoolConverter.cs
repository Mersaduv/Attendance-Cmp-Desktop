using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AttandenceDesktop.Converters;

public class BoolToBoolConverter : IValueConverter
{
    /// <summary>
    /// Whether to invert the boolean value during conversion
    /// </summary>
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return Invert ? !boolValue : boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return Invert ? !boolValue : boolValue;
        }
        return false;
    }
} 