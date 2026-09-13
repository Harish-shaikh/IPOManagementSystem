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
