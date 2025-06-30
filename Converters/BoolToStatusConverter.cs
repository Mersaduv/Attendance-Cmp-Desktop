using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace AttandenceDesktop.Converters;

/// <summary>
/// Converts a boolean (fingerprint registered) to Persian status text.
/// </summary>
public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "Registered" : "Not Registered";
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 