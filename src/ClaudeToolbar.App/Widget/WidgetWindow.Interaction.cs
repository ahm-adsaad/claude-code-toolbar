using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Widget;

public partial class WidgetWindow
{
    private readonly Popup _flyout = new()
    {
        AllowsTransparency = true,
        StaysOpen = true,
        Placement = PlacementMode.Top,
        PopupAnimation = PopupAnimation.Fade,
        VerticalOffset = -8,
    };
    private readonly StackPanel _flyoutPanel = new();
    private readonly Border _flyoutBorder = new()
    {
        Padding = new Thickness(12, 8, 12, 8),
        CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1),
    };
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public event Action? Clicked;
    public event Action? MenuRequested;
    public event Action? FlyoutRequested;

    public bool IsFlyoutOpen => _flyout.IsOpen;

    private void InitializeInteraction()
    {
        _flyoutBorder.Child = _flyoutPanel;
        _flyout.Child = _flyoutBorder;
        _flyout.PlacementTarget = Root;
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            FlyoutRequested?.Invoke();
        };
        Root.MouseEnter += (_, _) => _hoverTimer.Start();
        Root.MouseLeave += (_, _) =>
        {
            _hoverTimer.Stop();
            HideFlyout();
        };
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

    public void ShowFlyout(FlyoutModel model, WidgetTheme theme)
    {
        _flyoutBorder.Background = theme.Background;
        _flyoutBorder.BorderBrush = theme.BarTrack;
        _flyoutPanel.Children.Clear();
        foreach (var line in model.Lines)
        {
            _flyoutPanel.Children.Add(new TextBlock
            {
                Text = line,
                Foreground = theme.Text,
                FontFamily = WidgetTheme.Font,
                FontSize = theme.FontSize + 1,
                Margin = new Thickness(0, 1, 0, 1),
            });
        }
        _flyoutPanel.Children.Add(new TextBlock
        {
            Text = model.StatusText,
            Foreground = theme.Text,
            Opacity = 0.7,
            FontFamily = WidgetTheme.Font,
            FontSize = theme.FontSize,
            Margin = new Thickness(0, 4, 0, 0),
        });
        _flyout.IsOpen = true;
    }

    public void HideFlyout() => _flyout.IsOpen = false;
}
