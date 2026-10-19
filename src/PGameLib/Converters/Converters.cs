using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PGameLib.Helpers;
using PGameLib.Models;

namespace PGameLib.Converters;

/// <summary>bool -> Visibility. Pass ConverterParameter="invert" to flip.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Null/empty -> Collapsed, otherwise Visible. Parameter "invert" flips.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null && !(value is string s && string.IsNullOrWhiteSpace(s));
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Bytes (long) -> "12.4 GB".</summary>
public sealed class SizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long bytes ? SizeFormatter.Format(bytes) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>GameSource -> a short label.</summary>
public sealed class SourceLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is GameSource src
            ? src switch
            {
                GameSource.Steam => "Steam",
                GameSource.Epic => "Epic",
                GameSource.Custom => "Custom",
                _ => "Standalone"
            }
            : "Standalone";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>GameSource -> an accent brush for the little source pill.</summary>
public sealed class SourceBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is GameSource src
            ? src switch
            {
                GameSource.Steam => "#2A4766",
                GameSource.Epic => "#3A3A3A",
                GameSource.Custom => "#4A3A66",
                _ => "#394150"
            }
            : "#394150";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Loads an image from a file path into a BitmapImage without locking the file.
/// Returns null (so the Image falls back to a placeholder) when the path is empty or unreadable.
/// </summary>
public sealed class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;      // read fully, then release the file
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();                                    // safe to share across threads
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Returns true when the bound string path exists as a file; used to pick a placeholder.</summary>
public sealed class HasImageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string p && !string.IsNullOrWhiteSpace(p) && File.Exists(p);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Compares the bound value to the parameter and returns a bool (for RadioButton-style toggles).</summary>
public sealed class EqualityToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? parameter! : Binding.DoNothing;
}

/// <summary>
/// int Count -> Visibility. Default: Visible when count &gt; 0. Parameter "empty": Visible when count == 0.
/// Bound to ObservableCollection.Count, which raises change notifications, so the empty state updates live.
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var wantEmpty = string.Equals(parameter as string, "empty", StringComparison.OrdinalIgnoreCase);
        var show = wantEmpty ? count == 0 : count > 0;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>SortField enum -> a friendly label for the sort dropdown.</summary>
public sealed class SortFieldLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Models.SortField f
            ? f switch
            {
                Models.SortField.SizeBytes => "Size",
                Models.SortField.LastPlayed => "Last played",
                Models.SortField.AddedDate => "Date added",
                Models.SortField.Source => "Source",
                _ => "Name"
            }
            : value?.ToString() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>DateTime? (UTC) -> local "never" / short date string.</summary>
public sealed class LastPlayedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? dt.ToLocalTime().ToString("d MMM yyyy", culture) : "Never played";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
