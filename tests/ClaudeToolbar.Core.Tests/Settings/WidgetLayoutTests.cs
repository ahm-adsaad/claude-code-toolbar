using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class WidgetLayoutTests
{
    [Theory]
    [InlineData("RINGS", "rings")]
    [InlineData(" bars ", "bars")]
    [InlineData("rows", "rows")]
    [InlineData(null, "rings")]
    [InlineData("", "rings")]
    [InlineData("grid", "rings")]
    public void Normalize_AcceptsKnownLayoutsAndFallsBackToRings(string? input, string expected) =>
        Assert.Equal(expected, WidgetLayout.Normalize(input));

    [Fact]
    public void DefaultIsRings() => Assert.Equal(WidgetLayout.Rings, AppSettings.CreateDefault().Appearance.Layout);

    [Fact]
    public void Presets_LeaveTheLayoutAlone()
    {
        var a = new AppearanceSettings { Layout = WidgetLayout.Rows };
        Assert.True(Presets.TryApply("dark", a));
        Assert.Equal(WidgetLayout.Rows, a.Layout);
    }
}
