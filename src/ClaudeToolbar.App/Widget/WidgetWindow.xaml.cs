using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeToolbar.Core.Layout;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Widget;

public partial class WidgetWindow : Window
{
    private readonly UsageRowsControl _rows = new();
    private HwndSource? _source;

    public WidgetWindow()
    {
        InitializeComponent();
        Root.Children.Add(_rows);
        InitializeInteraction();
        // Nothing is hidden or acknowledged here: acknowledging before the jump would demote the very
        // state that decides where the click goes. The app jumps first, then closes the flyout.
        _rows.MascotClicked += () => MascotClicked?.Invoke();
    }

    public IntPtr Handle { get; private set; }

    public bool IsShown { get; private set; }

    /// <summary>Raw window messages (msg id) for the taskbar tracker.</summary>
    public event Action<int>? ShellMessage;

    /// <summary>A left click on Clawd. The flyout is still up and nothing is acknowledged yet.</summary>
    public event Action? MascotClicked;

    public void SetMascotTooltip(string? text) => _rows.SetMascotTooltip(text);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        var ex = GetWindowLongPtr(Handle, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(Handle, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST));
        _source = HwndSource.FromHwnd(Handle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        ShellMessage?.Invoke(msg);
        return IntPtr.Zero;
    }

    public void Render(WidgetModel model, RowSettings rows, WidgetTheme theme) => _rows.Render(model, rows, theme);

    public void UpdateTimes(WidgetModel model) => _rows.UpdateTimes(model);

    public void UpdateMascot(MascotModel model) => _rows.SetMascot(model);

    public (int Width, int Height) PhysicalSize()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return ((int)Math.Round(ActualWidth * dpi.DpiScaleX), (int)Math.Round(ActualHeight * dpi.DpiScaleY));
    }

    public void SetMaxPhysicalHeight(int physical)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var logical = physical / dpi.DpiScaleY;
        if (Math.Abs(MaxHeight - logical) > 0.5) MaxHeight = logical;
    }

    public RectI CurrentRect()
    {
        if (Handle == IntPtr.Zero || !GetWindowRect(Handle, out var r)) return default;
        return new RectI(r.Left, r.Top, r.Right, r.Bottom);
    }

    public void MoveTo(RectI target)
    {
        if (Handle == IntPtr.Zero) return;
        SetWindowPos(Handle, HWND_TOPMOST, target.Left, target.Top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public void AssertTopmost()
    {
        if (Handle == IntPtr.Zero) return;
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public void ShowNoActivate()
    {
        if (IsShown) return;
        Show();
        IsShown = true;
    }

    public void HideWidget()
    {
        HideFlyout();
        if (!IsShown) return;
        Hide();
        IsShown = false;
    }
}
