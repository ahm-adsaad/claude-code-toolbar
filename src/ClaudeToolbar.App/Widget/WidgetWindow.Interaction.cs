using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Widget;

public partial class WidgetWindow
{
    private static readonly Brush HitBridge = FrozenBrush("#01000000");

    private readonly Popup _flyout = new()
    {
        AllowsTransparency = true,
        StaysOpen = true,
        Placement = PlacementMode.Top,
        PopupAnimation = PopupAnimation.Fade,
        VerticalOffset = 0,
    };
    private readonly StackPanel _flyoutPanel = new();
    private readonly Border _flyoutBorder = new()
    {
        Padding = new Thickness(12, 8, 12, 8),
        CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1),
        // The 8 px below the border are part of the popup window, so the mouse can cross from the widget without leaving it.
        Margin = new Thickness(0, 0, 0, 8),
    };
    private readonly Grid _flyoutRoot = new() { Background = HitBridge };
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public event Action? Clicked;
    public event Action? MenuRequested;
    public event Action? FlyoutRequested;
    public event Action? HoverStarted;
    /// <summary>A click on one session line in the flyout; the flyout hides right after.</summary>
    public event Action<string>? SessionClicked;

    /// <summary>Raised when a flyout that was on screen is dismissed — the user has read it by then.</summary>
    public event Action? FlyoutHidden;

    public bool IsFlyoutOpen => _flyout.IsOpen;

    private void InitializeInteraction()
    {
        _flyoutBorder.Child = _flyoutPanel;
        _flyoutRoot.Children.Add(_flyoutBorder);
        _flyout.Child = _flyoutRoot;
        _flyout.PlacementTarget = Root;
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            FlyoutRequested?.Invoke();
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideFlyout();
        };
        Root.MouseEnter += (_, _) =>
        {
            _hideTimer.Stop();
            _hoverTimer.Start();
            HoverStarted?.Invoke();
        };
        Root.MouseLeave += (_, _) =>
        {
            _hoverTimer.Stop();
            if (_flyout.IsOpen) _hideTimer.Start();
        };
        _flyoutRoot.MouseEnter += (_, _) => _hideTimer.Stop();
        _flyoutRoot.MouseLeave += (_, _) => _hideTimer.Start();
        Root.MouseLeftButtonUp += (_, _) =>
        {
            HideFlyout();
            Clicked?.Invoke();
        };
        Root.MouseRightButtonUp += (_, _) =>
        {
            HideFlyout();
            MenuRequested?.Invoke();
        };
    }

    public void ShowFlyout(FlyoutModel model, IReadOnlyList<SessionLineItem> sessions, string? hint, WidgetTheme theme)
    {
        _flyoutBorder.Background = theme.Background;
        _flyoutBorder.BorderBrush = theme.BarTrack;
        _flyoutPanel.Children.Clear();
        foreach (var line in model.Lines)
            _flyoutPanel.Children.Add(MakeLine(line, theme, theme.FontSize + 1, 1.0, new Thickness(0, 1, 0, 1)));
        foreach (var session in sessions)
            _flyoutPanel.Children.Add(MakeSessionLine(session, theme));
        if (hint is not null)
            _flyoutPanel.Children.Add(MakeLine(hint, theme, theme.FontSize, 0.7, new Thickness(0, 1, 0, 1)));
        _flyoutPanel.Children.Add(MakeLine(model.StatusText, theme, theme.FontSize, 0.7, new Thickness(0, 4, 0, 0)));
        _flyout.IsOpen = true;
    }

    private static TextBlock MakeLine(string text, WidgetTheme theme, double size, double opacity, Thickness margin) => new()
    {
        Text = text,
        Foreground = theme.Text,
        Opacity = opacity,
        FontFamily = WidgetTheme.Font,
        FontSize = size,
        Margin = margin,
    };

    private TextBlock MakeSessionLine(SessionLineItem session, WidgetTheme theme)
    {
        var block = MakeLine(session.Text, theme, theme.FontSize + 1, 1.0, new Thickness(0, 1, 0, 1));
        block.Cursor = Cursors.Hand;
        block.ToolTip = "Go to this session";
        block.MouseEnter += (_, _) => block.TextDecorations = TextDecorations.Underline;
        block.MouseLeave += (_, _) => block.TextDecorations = null;
        block.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            HideFlyout();
            SessionClicked?.Invoke(session.Id);
        };
        return block;
    }

    public void HideFlyout()
    {
        _hideTimer.Stop();
        if (!_flyout.IsOpen) return;
        _flyout.IsOpen = false;
        FlyoutHidden?.Invoke();
    }

    private static SolidColorBrush FrozenBrush(string argb)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(argb));
        brush.Freeze();
        return brush;
    }
}
