using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AttandenceDesktop.Converters
{
    public class DateTimeToDateTimeOffsetConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                return (DateTimeOffset)dt;
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTimeOffset dto)
            {
                return dto.DateTime;
            }
            return null;
        }
    }
} 