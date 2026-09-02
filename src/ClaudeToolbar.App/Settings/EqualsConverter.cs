using System.Globalization;
using System.Windows.Data;

namespace ClaudeToolbar.App.Settings;

/// <summary>Binds a string property to a RadioButton: checked when the value equals the parameter.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter?.ToString() ?? string.Empty : Binding.DoNothing;
}
