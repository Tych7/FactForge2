using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FactForge.Converters;

// Lets XAML do `IsVisible="{Binding Phase, Converter={StaticResource EnumEqualityConverter}, ConverterParameter=Lobby}"`.
public class EnumEqualityConverter : IValueConverter
{
    public static readonly EnumEqualityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
