using System.Windows.Media;
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Widget;

public sealed class WidgetTheme
{
    public required Brush Background { get; init; }
    public required Brush Text { get; init; }
    public required Brush BarTrack { get; init; }
    public required Brush BarOk { get; init; }
    public required Brush BarWarn { get; init; }
    public required Brush BarCrit { get; init; }
    public required double FontSize { get; init; }
    public required double CornerRadius { get; init; }

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");

    public static WidgetTheme FromSettings(AppearanceSettings a) => new()
    {
        Background = BrushFrom(a.Background),
        Text = BrushFrom(a.Text),
        BarTrack = BrushFrom(a.BarTrack),
        BarOk = BrushFrom(a.BarOk),
        BarWarn = BrushFrom(a.BarWarn),
        BarCrit = BrushFrom(a.BarCrit),
        FontSize = a.FontSize,
        CornerRadius = a.CornerRadius,
    };

    public Brush BrushFor(BarLevel level) => level switch
    {
        BarLevel.Ok => BarOk,
        BarLevel.Warn => BarWarn,
        _ => BarCrit,
    };

    public static SolidColorBrush BrushFrom(string argb)
    {
        var color = (Color)ColorConverter.ConvertFromString(argb);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
