using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Mascot;

public class MascotModelBuilderTests
{
    private static WidgetModel Widget(bool dimmed = false, params BarLevel[] levels) =>
        new(levels.Select((l, i) => new WidgetRow($"r{i}", 10, "10%", "", l)).ToList(), dimmed, false, null);

    [Fact]
    public void OffModeHidesTheMascot() =>
        Assert.Equal(MascotModel.Hidden, MascotModelBuilder.Build(Widget(false, BarLevel.Ok), "off", 12));

    [Fact]
    public void NoticeWithoutRowsHidesTheMascot() =>
        Assert.Equal(MascotModel.Hidden, MascotModelBuilder.Build(new WidgetModel([], false, false, "Sign in with claude"), "full", 12));

    [Fact]
    public void WorstLevelWinsAndArmAnglePassesThrough()
    {
        var m = MascotModelBuilder.Build(Widget(false, BarLevel.Ok, BarLevel.Crit, BarLevel.Warn), "full", 33);
        Assert.True(m.Visible);
        Assert.Equal(BarLevel.Crit, m.Level);
        Assert.Equal(33, m.ArmAngle);
        Assert.False(m.Dimmed);
    }

    [Fact]
    public void HoverModeIsVisibleAndDimmedFollowsTheWidget()
    {
        var m = MascotModelBuilder.Build(Widget(true, BarLevel.Ok), "hover", WaveAnimation.RestAngle);
        Assert.True(m.Visible);
        Assert.Equal(BarLevel.Ok, m.Level);
        Assert.True(m.Dimmed);
    }

    [Fact]
    public void UnknownModeBehavesLikeFull() =>
        Assert.True(MascotModelBuilder.Build(Widget(false, BarLevel.Ok), "sparkles", 0).Visible);
}
