using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Mascot;

public class ThresholdCueTrackerTests
{
    private static readonly DateTimeOffset Reset = new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);

    private static TrackedRow Row(string key, BarLevel level, DateTimeOffset? reset = null) => new(key, level, reset ?? Reset);

    [Fact]
    public void FirstObservationOnlyPrimes()
    {
        var t = new ThresholdCueTracker();
        Assert.Null(t.Observe([Row("5h", BarLevel.Crit)]));
    }

    [Fact]
    public void NoChangeGivesNoCue()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Warn)]);
        Assert.Null(t.Observe([Row("5h", BarLevel.Warn)]));
    }

    [Theory]
    [InlineData(BarLevel.Ok, BarLevel.Warn, MascotCue.Warn)]
    [InlineData(BarLevel.Ok, BarLevel.Crit, MascotCue.Crit)]
    [InlineData(BarLevel.Warn, BarLevel.Crit, MascotCue.Crit)]
    public void RiseEmitsCue(BarLevel from, BarLevel to, MascotCue expected)
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", from)]);
        Assert.Equal(expected, t.Observe([Row("5h", to)]));
    }

    [Fact]
    public void StrongestCueWinsAcrossRows()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Ok), Row("7d", BarLevel.Ok)]);
        Assert.Equal(MascotCue.Crit, t.Observe([Row("5h", BarLevel.Warn), Row("7d", BarLevel.Crit)]));
    }

    [Fact]
    public void DropEmitsNothingAndReprimes()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Crit)]);
        Assert.Null(t.Observe([Row("5h", BarLevel.Ok)]));
        Assert.Equal(MascotCue.Warn, t.Observe([Row("5h", BarLevel.Warn)]));
    }

    [Fact]
    public void NewPeriodReprimesWithoutCue()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Ok)]);
        Assert.Null(t.Observe([Row("5h", BarLevel.Crit, Reset.AddHours(5))]));
        Assert.Null(t.Observe([Row("5h", BarLevel.Crit, Reset.AddHours(5))]));
    }

    [Fact]
    public void MissingResetDateStillCues()
    {
        var t = new ThresholdCueTracker();
        t.Observe([new TrackedRow("7d Opus", BarLevel.Ok, null)]);
        Assert.Equal(MascotCue.Warn, t.Observe([new TrackedRow("7d Opus", BarLevel.Warn, null)]));
    }

    [Fact]
    public void RowThatDisappearsIsReprimedWhenItReturns()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Ok), Row("7d", BarLevel.Ok)]);
        t.Observe([Row("5h", BarLevel.Ok)]);
        Assert.Null(t.Observe([Row("5h", BarLevel.Ok), Row("7d", BarLevel.Crit)]));
    }

    [Fact]
    public void SameLevelDoesNotRepeatTheCue()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Ok)]);
        t.Observe([Row("5h", BarLevel.Crit)]);
        Assert.Null(t.Observe([Row("5h", BarLevel.Crit)]));
    }

    [Fact]
    public void ResetClearsHistory()
    {
        var t = new ThresholdCueTracker();
        t.Observe([Row("5h", BarLevel.Ok)]);
        t.Reset();
        Assert.Null(t.Observe([Row("5h", BarLevel.Crit)]));
    }

    [Fact]
    public void TrackedRowsMapLabelsToTheirWindows()
    {
        var snapshot = new UsageSnapshot(
            new UsageWindow(42, Reset),
            new UsageWindow(18, Reset.AddDays(3)),
            new UsageWindow(75, null),
            null,
            Reset.AddHours(-1));
        var widget = new WidgetModel(
        [
            new WidgetRow("5h", 42, "42%", "2h", BarLevel.Ok),
            new WidgetRow("7d", 18, "18%", "3d", BarLevel.Ok),
            new WidgetRow("7d Opus", 75, "75%", "", BarLevel.Warn),
            new WidgetRow("7d Sonnet", 0, "—", "", BarLevel.Ok),
        ], false, false, null);

        var rows = TrackedRows.From(widget, snapshot);

        Assert.Equal(4, rows.Count);
        Assert.Equal(new TrackedRow("5h", BarLevel.Ok, Reset), rows[0]);
        Assert.Equal(new TrackedRow("7d", BarLevel.Ok, Reset.AddDays(3)), rows[1]);
        Assert.Equal(new TrackedRow("7d Opus", BarLevel.Warn, null), rows[2]);
        Assert.Equal(new TrackedRow("7d Sonnet", BarLevel.Ok, null), rows[3]);
        Assert.Empty(TrackedRows.From(new WidgetModel([], false, false, "x"), snapshot));
    }
}
