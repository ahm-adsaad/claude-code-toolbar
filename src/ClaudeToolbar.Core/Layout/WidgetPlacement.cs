namespace ClaudeToolbar.Core.Layout;

public static class WidgetPlacement
{
    public const int VerticalMargin = 4;

    /// <summary>Places the widget so its right edge is <paramref name="gap"/> px left of the notification area, centered in the taskbar.</summary>
    public static RectI Compute(RectI taskbarNow, RectI notifyArea, int widgetWidth, int widgetHeight, int gap)
    {
        var right = notifyArea.Left - gap;
        var left = right - widgetWidth;
        var top = taskbarNow.Top + (taskbarNow.Height - widgetHeight) / 2;
        return new RectI(left, top, right, top + widgetHeight);
    }

    /// <summary>True when less than half of the taskbar is inside the monitor (auto-hide slid away).</summary>
    public static bool IsTaskbarMostlyHidden(RectI taskbarNow, RectI monitor)
    {
        var visibleTop = Math.Max(taskbarNow.Top, monitor.Top);
        var visibleBottom = Math.Min(taskbarNow.Bottom, monitor.Bottom);
        var visible = Math.Max(0, visibleBottom - visibleTop);
        return visible < taskbarNow.Height / 2;
    }

    public static int MaxWidgetHeight(RectI taskbar) => Math.Max(0, taskbar.Height - VerticalMargin);
}
