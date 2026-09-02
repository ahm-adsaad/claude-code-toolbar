using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Widget;

/// <summary>Draws the stacked usage rows. Imperative rendering keeps the widget and the settings preview identical.</summary>
public sealed class UsageRowsControl : Border
{
    private const double BarHeight = 4;
    private const double Gap = 6;

    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _staleDot = new() { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Gap, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly List<(TextBlock Time, TextBlock Percent)> _live = new();

    public UsageRowsControl()
    {
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_staleDot, Dock.Right);
        panel.Children.Add(_staleDot);
        panel.Children.Add(_rows);
        Child = panel;
        Padding = new Thickness(8, 2, 8, 2);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
    }

    public void Render(WidgetModel model, RowSettings rows, WidgetTheme theme)
    {
        Background = theme.Background;
        CornerRadius = new CornerRadius(theme.CornerRadius);
        Opacity = model.Dimmed ? 0.5 : 1.0;
        _staleDot.Fill = theme.BarWarn;
        _staleDot.Visibility = model.ShowStaleDot ? Visibility.Visible : Visibility.Collapsed;

        _rows.Children.Clear();
        _live.Clear();

        if (model.Rows.Count == 0)
        {
            _rows.Children.Add(MakeText(model.Notice ?? string.Empty, theme, 0));
            return;
        }

        foreach (var row in model.Rows)
            _rows.Children.Add(MakeRow(row, rows, theme));
    }

    public void UpdateTimes(WidgetModel model)
    {
        for (var i = 0; i < _live.Count && i < model.Rows.Count; i++)
        {
            var (time, percent) = _live[i];
            if (time.Text != model.Rows[i].TimeText) time.Text = model.Rows[i].TimeText;
            if (percent.Text != model.Rows[i].PercentText) percent.Text = model.Rows[i].PercentText;
        }
    }

    private FrameworkElement MakeRow(WidgetRow row, RowSettings rows, WidgetTheme theme)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = MakeText(row.Label, theme, 0);
        label.MinWidth = 18;
        label.Visibility = rows.ShowLabel ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var track = new Border
        {
            Width = rows.BarWidth,
            Height = BarHeight,
            CornerRadius = new CornerRadius(BarHeight / 2),
            Background = theme.BarTrack,
            Margin = new Thickness(Gap, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = rows.ShowBar ? Visibility.Visible : Visibility.Collapsed,
            Child = new Border
            {
                Width = rows.BarWidth * Math.Clamp(row.Utilization, 0, 100) / 100.0,
                Height = BarHeight,
                CornerRadius = new CornerRadius(BarHeight / 2),
                Background = theme.BrushFor(row.Level),
                HorizontalAlignment = HorizontalAlignment.Left,
            },
        };
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        var percent = MakeText(row.PercentText, theme, Gap);
        percent.MinWidth = 30;
        percent.TextAlignment = TextAlignment.Right;
        percent.Visibility = rows.ShowPercent ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(percent, 2);
        grid.Children.Add(percent);

        var time = MakeText(row.TimeText, theme, Gap);
        time.MinWidth = 40;
        time.Visibility = rows.ShowTime ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(time, 3);
        grid.Children.Add(time);

        _live.Add((time, percent));
        return grid;
    }

    private static TextBlock MakeText(string text, WidgetTheme theme, double leftMargin) => new()
    {
        Text = text,
        Foreground = theme.Text,
        FontFamily = WidgetTheme.Font,
        FontSize = theme.FontSize,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(leftMargin, 0, 0, 0),
    };
}
