using ClaudeToolbar.Core.Layout;

namespace ClaudeToolbar.Core.Tests.Layout;

public class WidgetPlacementTests
{
    private static readonly RectI Taskbar = new(0, 1392, 2560, 1440);
    private static readonly RectI Notify = new(2200, 1392, 2560, 1440);

    [Fact]
    public void RightEdgeSitsGapLeftOfTrayAndVerticallyCentered()
    {
        var r = WidgetPlacement.Compute(Taskbar, Notify, widgetWidth: 180, widgetHeight: 40, gap: 8);
        Assert.Equal(2192, r.Right);
        Assert.Equal(2012, r.Left);
        Assert.Equal(1396, r.Top);
        Assert.Equal(1436, r.Bottom);
        Assert.Equal(180, r.Width);
        Assert.Equal(40, r.Height);
    }

    [Fact]
    public void OddRemainderRoundsDown()
    {
        var r = WidgetPlacement.Compute(Taskbar, Notify, 100, 41, 0);
        Assert.Equal(1395, r.Top);
    }

    [Fact]
    public void MaxHeightLeavesFourPixels()
    {
        Assert.Equal(44, WidgetPlacement.MaxWidgetHeight(Taskbar));
    }

    [Fact]
    public void HiddenTaskbarDetected()
    {
        var monitor = new RectI(0, 0, 2560, 1440);
        Assert.False(WidgetPlacement.IsTaskbarMostlyHidden(Taskbar, monitor));
        Assert.True(WidgetPlacement.IsTaskbarMostlyHidden(new RectI(0, 1438, 2560, 1486), monitor));
        Assert.False(WidgetPlacement.IsTaskbarMostlyHidden(new RectI(0, 1410, 2560, 1458), monitor));
    }
}
