using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Mascot;

public class MascotModelBuilderTests
{
    private static WidgetModel Widget(bool dimmed = false) =>
        new([new WidgetRow("5h", 10, "10%", "", BarLevel.Ok)], dimmed, false, null);

    [Fact]
    public void OffModeHidesTheMascot() =>
        Assert.Equal(MascotModel.Hidden, MascotModelBuilder.Build(Widget(), "off", 12, MascotBadge.Attention));

    [Fact]
    public void NoticeWithoutRowsHidesTheMascot() =>
        Assert.Equal(MascotModel.Hidden, MascotModelBuilder.Build(new WidgetModel([], false, false, "Sign in with claude"), "full", 12));

    [Fact]
    public void ArmAngleBadgeAndDimmingPassThrough()
    {
        var m = MascotModelBuilder.Build(Widget(dimmed: true), "hover", 33, MascotBadge.Finished, badgeLit: false);
        Assert.True(m.Visible);
        Assert.Equal(33, m.ArmAngle);
        Assert.True(m.Dimmed);
        Assert.Equal(MascotBadge.Finished, m.Badge);
        Assert.False(m.BadgeLit);
    }

    [Fact]
    public void DefaultsAreNoBadgeAndLit()
    {
        var m = MascotModelBuilder.Build(Widget(), "full", WaveAnimation.RestAngle);
        Assert.Equal(MascotBadge.None, m.Badge);
        Assert.True(m.BadgeLit);
    }

    [Fact]
    public void BadgeStrengthOrder() =>
        Assert.Equal(MascotBadge.Attention, new[] { MascotBadge.Working, MascotBadge.Attention, MascotBadge.Failed, MascotBadge.Finished }.Max());
}
