using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Collections.Generic;

namespace AttandenceDesktop.Converters;

public class FingerNumberToNameConverter : IValueConverter
{
    private static readonly Dictionary<int, string> FingerNames = new Dictionary<int, string>
    {
        { 0, "Left Little Finger" },
        { 1, "Left Ring Finger" },
        { 2, "Left Middle Finger" },
        { 3, "Left Index Finger" },
        { 4, "Left Thumb" },
        { 5, "Right Thumb" },
        { 6, "Right Index Finger" },
        { 7, "Right Middle Finger" },
        { 8, "Right Ring Finger" },
        { 9, "Right Little Finger" }
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int fingerNumber && FingerNames.TryGetValue(fingerNumber, out string? name))
        {
            return name;
        }
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // We don't need to convert back from name to number
        return 0;
    }
} 