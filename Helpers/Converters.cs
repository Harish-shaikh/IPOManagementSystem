using System.Globalization;

namespace IPOProcessingSystem.Helpers;

/// <summary>Two-way converter that treats a RadioButton's IsChecked as "does the
/// bound string equal ConverterParameter". Lets multiple RadioButtons bind to a
/// single string property (Exchange, AsbaType, IssueType, ...).</summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? parameter?.ToString() : Binding.DoNothing;
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Returns true when the bound string is non-null and non-empty.
/// Used by IsVisible on status/error Label elements throughout the app.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value?.ToString());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Highlights the active tab button. When the bound string (ActiveTab) equals
/// ConverterParameter the active colour is returned; otherwise a muted colour.
/// </summary>
public class TabColorConverter : IValueConverter
{
    // Active tab colour — matches the app's Primary resource.
    private static readonly Color ActiveColor  = Color.FromArgb("#1565C0"); // deep blue
    private static readonly Color InactiveColor = Color.FromArgb("#90A4AE"); // slate-grey

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var active = value?.ToString();
        var tab    = parameter?.ToString();
        return string.Equals(active, tab, StringComparison.OrdinalIgnoreCase)
            ? (object)ActiveColor
            : InactiveColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
