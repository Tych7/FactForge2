using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FactForge.Helpers;

namespace FactForge.Converters;

public class PlayerColorConverter : IValueConverter
{
    public static readonly PlayerColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string playerName)
        {
            return PlayerColorHelper.GetColorForName(playerName);
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}