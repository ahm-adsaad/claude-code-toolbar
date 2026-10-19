using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Widget;

/// <summary>
/// Draws the usage limits as rings, upright bars or stacked rows, by <see cref="WidgetTheme.Layout"/>. Imperative
/// rendering keeps the widget and the settings preview identical.
/// </summary>
public sealed class UsageRowsControl : Border
{
    private const double BarHeight = 4;
    private const double Gap = 6;
    private const double CellGap = 10;
    private const double TimeMinWidth = 40;
    // The widget's inner height is 34 px: a ring of 30 leaves room for nothing else, and its label must stay legible.
    private const double RingSize = 30;
    private const double RingThickness = 3;
    private const double RingLabelSize = 9;
    private const double UprightWidth = 4;
    private const double UprightHeight = 28;

    private readonly StackPanel _columns = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _staleDot = new() { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Gap, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly MascotControl _mascot = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Visibility = Visibility.Collapsed };
    private readonly List<(TextBlock Time, TextBlock Percent)> _live = new();

    /// <summary>A left click on Clawd. Handled here so the widget's own click (which opens Settings) does not also fire.</summary>
    public event Action? MascotClicked;

    private string? _mascotTooltip;
    private RowSettings? _settings;
    private WidgetTheme? _theme;

    public UsageRowsControl()
    {
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_staleDot, Dock.Right);
        panel.Children.Add(_staleDot);
        DockPanel.SetDock(_mascot, Dock.Left);
        panel.Children.Add(_mascot);
        _mascot.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            MascotClicked?.Invoke();
        };
        panel.Children.Add(_columns);
        Child = panel;
        Padding = new Thickness(8, 2, 8, 2);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
    }

    public void Render(WidgetModel model, RowSettings rows, WidgetTheme theme)
    {
        _settings = rows;
        _theme = theme;
        Background = theme.Background;
        CornerRadius = new CornerRadius(theme.CornerRadius);
        Opacity = model.Dimmed ? 0.5 : 1.0;
        _staleDot.Fill = theme.BarWarn;
        _staleDot.Visibility = model.ShowStaleDot ? Visibility.Visible : Visibility.Collapsed;

        _columns.Children.Clear();
        _live.Clear();

        if (model.Rows.Count == 0)
        {
            _columns.Children.Add(MakeText(model.Notice ?? string.Empty, theme, 0));
            return;
        }

        for (var i = 0; i < model.Rows.Count; i++)
        {
            var row = model.Rows[i];
            var left = i == 0 ? 0 : CellGap;
            switch (theme.Layout)
            {
                case WidgetLayout.Rows:
                    // Columns of two: a third row never stacks under the first two.
                    if (i % 2 == 0)
                        _columns.Children.Add(new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(left, 0, 0, 0) });
                    ((StackPanel)_columns.Children[^1]).Children.Add(MakeRow(row, rows, theme));
                    break;
                case WidgetLayout.Bars:
                    _columns.Children.Add(MakeUprightCell(row, rows, theme, left));
                    break;
                default:
                    _columns.Children.Add(MakeRingCell(row, rows, theme, left));
                    break;
            }
        }
    }

    public void SetMascot(MascotModel model) => _mascot.Update(model);

    public void SetMascotTooltip(string? text)
    {
        if (text == _mascotTooltip) return;
        _mascotTooltip = text;
        _mascot.ToolTip = text;
        _mascot.Cursor = text is null ? null : Cursors.Hand;
    }

    public void UpdateTimes(WidgetModel model)
    {
        for (var i = 0; i < _live.Count && i < model.Rows.Count; i++)
        {
            var (time, percent) = _live[i];
            if (_settings is not null && _theme is not null) SetTime(time, model.Rows[i], _settings, _theme);
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

        var time = MakeText(string.Empty, theme, Gap);
        time.MinWidth = TimeMinWidth;
        SetTime(time, row, rows, theme);
        Grid.SetColumn(time, 3);
        grid.Children.Add(time);

        _live.Add((time, percent));
        return grid;
    }

    /// <summary>A ring filling clockwise with the short label inside, then the percentage over the time column.</summary>
    private FrameworkElement MakeRingCell(WidgetRow row, RowSettings rows, WidgetTheme theme, double left)
    {
        var cell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(left, 0, 0, 0) };
        if (rows.ShowBar)
        {
            var ring = new Grid { Width = RingSize, Height = RingSize, VerticalAlignment = VerticalAlignment.Center };
            ring.Children.Add(new Ellipse { Stroke = theme.BarTrack, StrokeThickness = RingThickness });
            ring.Children.Add(new Path
            {
                Data = RingGeometry.Arc(row.Utilization / 100.0, RingSize, RingThickness),
                Stroke = theme.BrushFor(row.Level),
                StrokeThickness = RingThickness,
            });
            if (rows.ShowLabel)
            {
                var label = MakeText(row.ShortLabel, theme, 0);
                label.FontSize = RingLabelSize;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                ring.Children.Add(label);
            }
            cell.Children.Add(ring);
        }
        else if (rows.ShowLabel)
        {
            // Without the ring the label has nothing to sit in, so it leads the cell at the normal size.
            cell.Children.Add(MakeText(row.ShortLabel, theme, 0));
        }
        cell.Children.Add(MakeLines(null, row, rows, theme, cell.Children.Count > 0));
        return cell;
    }

    /// <summary>A thin bar filling upward, then the short label and percentage over the time column.</summary>
    private FrameworkElement MakeUprightCell(WidgetRow row, RowSettings rows, WidgetTheme theme, double left)
    {
        var cell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(left, 0, 0, 0) };
        if (rows.ShowBar)
        {
            cell.Children.Add(new Border
            {
                Width = UprightWidth,
                Height = UprightHeight,
                CornerRadius = new CornerRadius(UprightWidth / 2),
                Background = theme.BarTrack,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Border
                {
                    Height = UprightHeight * Math.Clamp(row.Utilization, 0, 100) / 100.0,
                    CornerRadius = new CornerRadius(UprightWidth / 2),
                    Background = theme.BrushFor(row.Level),
                    VerticalAlignment = VerticalAlignment.Bottom,
                },
            });
        }
        cell.Children.Add(MakeLines(rows.ShowLabel ? row.ShortLabel : null, row, rows, theme, rows.ShowBar));
        return cell;
    }

    /// <summary>The two lines of a compact cell: the percentage (after <paramref name="label"/>, if any) over the time column.</summary>
    private StackPanel MakeLines(string? label, WidgetRow row, RowSettings rows, WidgetTheme theme, bool indent)
    {
        var top = new StackPanel { Orientation = Orientation.Horizontal };
        if (label is not null) top.Children.Add(MakeText(label, theme, 0));
        var percent = MakeText(row.PercentText, theme, label is null ? 0 : 4);
        percent.Visibility = rows.ShowPercent ? Visibility.Visible : Visibility.Collapsed;
        top.Children.Add(percent);

        var time = MakeText(string.Empty, theme, 0);
        time.MinWidth = TimeMinWidth;
        SetTime(time, row, rows, theme);

        _live.Add((time, percent));
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(indent ? 5 : 0, 0, 0, 0),
            Children = { top, time },
        };
    }

    /// <summary>The reset countdown, or the pace warning in its colour; the warning shows even with "Show time" off.</summary>
    private static void SetTime(TextBlock time, WidgetRow row, RowSettings rows, WidgetTheme theme)
    {
        var text = row.Warning ?? row.TimeText;
        if (time.Text != text) time.Text = text;
        var brush = row.Warning is null ? theme.Text : theme.BrushFor(row.WarningLevel);
        if (!ReferenceEquals(time.Foreground, brush)) time.Foreground = brush;
        var visibility = rows.ShowTime || row.Warning is not null ? Visibility.Visible : Visibility.Collapsed;
        if (time.Visibility != visibility) time.Visibility = visibility;
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
