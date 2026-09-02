using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Settings;

public partial class ColorEditor : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(ColorEditor),
        new FrameworkPropertyMetadata("#FF000000", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    private bool _updating;

    public ColorEditor()
    {
        InitializeComponent();
        SyncFromValue();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ColorEditor)d).SyncFromValue();

    private void SyncFromValue()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var text = SettingsValidator.NormalizeColor(Value, "#FF000000");
            var color = (Color)ColorConverter.ConvertFromString(text);
            Hex.Text = text;
            SwatchFill.Background = new SolidColorBrush(color);
            var (h, s, v) = ToHsv(color);
            H.Value = h;
            S.Value = s * 100;
            V.Value = v * 100;
            A.Value = color.A / 255.0 * 100;
        }
        finally
        {
            _updating = false;
        }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e) => Picker.IsOpen = !Picker.IsOpen;

    private void Hex_LostFocus(object sender, RoutedEventArgs e) => CommitHex();

    private void Hex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitHex();
    }

    private void CommitHex()
    {
        var text = Hex.Text.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (SettingsValidator.IsValidColor(text) || SettingsValidator.NormalizeColor(text, "") != "")
            Value = SettingsValidator.NormalizeColor(text, Value);
        else
            SyncFromValue();
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var color = FromHsv(H.Value, S.Value / 100, V.Value / 100);
        color.A = (byte)Math.Round(A.Value / 100 * 255);
        Value = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static (double H, double S, double V) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }
        var s = max == 0 ? 0 : delta / max;
        return (h, s, max);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
