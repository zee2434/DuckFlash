using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DuckFlash.Converters;

/// <summary>
/// Konversi string menjadi Visibility: kosong/string kosong -> Collapsed,
/// lain lain -> Visible. Digunakan untuk menampilkan panel detail perangkat
/// kalau sudah ada data yang bisa ditampilkan.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        return string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
