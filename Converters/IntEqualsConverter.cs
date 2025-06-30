using System;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Data;

namespace AttandenceDesktop.Converters;

public class IntEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if(value is int selected && parameter is int current)
            return selected == current;
        if(value is int sel && parameter is string paramStr && int.TryParse(paramStr, out var cur))
            return sel == cur;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if(value is bool b && b && parameter != null)
        {
            if(parameter is int i) return i;
            if(parameter is string s && int.TryParse(s, out var i2)) return i2;
        }
        return BindingOperations.DoNothing;
    }
} 