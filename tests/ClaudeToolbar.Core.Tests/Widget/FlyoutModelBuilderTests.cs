using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Widget;

public class FlyoutModelBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly CredentialsState Creds = new CredentialsState.Valid("p", "t", Now.AddHours(7), "max");
    private static string Clock(DateTimeOffset t) => t.ToString("HH:mm");

    [Fact]
    public void ListsWindowsUpdatedAndStatus()
    {
        var snap = new UsageSnapshot(
            new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
            new UsageWindow(18, Now.AddDays(3).AddHours(4)),
            new UsageWindow(5, Now.AddDays(3)),
            null,
            null,
            Now.AddSeconds(-12));
        var state = new MonitorState(UsageStatus.Ok, snap, Now.AddSeconds(-12), null, Creds);
        var f = FlyoutModelBuilder.Build(state, Now, Clock);
        Assert.Equal("Session 42% · resets in 2h 13m · at 12:13", f.Lines[0].Text);
        Assert.Equal("Weekly 18% · resets in 3d 4h · at 14:00", f.Lines[1].Text);
        Assert.Equal("Weekly Opus 5% · resets in 3d 0h · at 10:00", f.Lines[2].Text);
        Assert.Equal("Updated 12s ago", f.Lines[3].Text);
        Assert.Equal(4, f.Lines.Count);
        Assert.Equal("OK", f.StatusText);
    }

    [Fact]
    public void ListsFableAfterSonnet()
    {
        var snap = new UsageSnapshot(
            new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
            null,
            null,
            new UsageWindow(3, Now.AddDays(2)),
            new UsageWindow(72, Now.AddDays(3).AddHours(4)),
            Now);
        var f = FlyoutModelBuilder.Build(new MonitorState(UsageStatus.Ok, snap, Now, null, Creds), Now, Clock);
        Assert.Equal("Weekly Sonnet 3% · resets in 2d 0h · at 10:00", f.Lines[1].Text);
        Assert.Equal("Weekly Fable 72% · resets in 3d 4h · at 14:00", f.Lines[2].Text);
    }

    private static MonitorState FableState(UsageStatus status = UsageStatus.Ok) => new(status, new UsageSnapshot(
        new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
        null,
        null,
        null,
        new UsageWindow(72, Now.AddDays(3).AddHours(4)),
        Now), Now, null, Creds);

    private static Dictionary<UsageLimit, PaceEstimate> FablePace(PaceEstimate e) => new() { [UsageLimit.SevenDayFable] = e };

    private static readonly PaceEstimate RunsOut = new(PaceVerdict.RunsOutFirst, TimeSpan.FromHours(6), null, 1, TimeSpan.FromHours(9), PaceBasis.Work);

    [Fact]
    public void Pace_RunsOutFirst_AddsAWarningAndItsBasisUnderTheLimit()
    {
        var f = FlyoutModelBuilder.Build(FableState(), Now, Clock, FablePace(RunsOut));
        Assert.Equal("Weekly Fable 72% · resets in 3d 4h · at 14:00", f.Lines[1].Text);
        Assert.Equal(new FlyoutLine("At your pace: about 6h of work left, runs out before the reset", BarLevel.Warn), f.Lines[2]);
        Assert.Equal(new FlyoutLine("Based on your last 9 hours of work"), f.Lines[3]);
        Assert.Equal("Updated 0s ago", f.Lines[4].Text);
        Assert.Null(f.Lines[0].Tint);
    }

    [Fact]
    public void Pace_Lasts()
    {
        var f = FlyoutModelBuilder.Build(FableState(), Now, Clock, FablePace(RunsOut with { Verdict = PaceVerdict.Lasts }));
        Assert.Equal(new FlyoutLine("At your pace this lasts until the reset"), f.Lines[2]);
        Assert.Equal("Based on your last 9 hours of work", f.Lines[3].Text);
    }

    [Fact]
    public void Pace_Learning_HasNoBasisLine()
    {
        var f = FlyoutModelBuilder.Build(FableState(), Now, Clock, FablePace(PaceEstimate.Learning));
        Assert.Equal(new FlyoutLine("Learning your pace"), f.Lines[2]);
        Assert.StartsWith("Updated", f.Lines[3].Text);
    }

    [Fact]
    public void Pace_UsedUp_AddsNothing()
    {
        var f = FlyoutModelBuilder.Build(FableState(), Now, Clock, FablePace(RunsOut with { Verdict = PaceVerdict.UsedUp }));
        Assert.StartsWith("Updated", f.Lines[2].Text);
    }

    [Fact]
    public void Pace_WithSeveralSessionsRunning()
    {
        var e = RunsOut with { WorkLeftNow = TimeSpan.FromMinutes(40), SessionsNow = 3 };
        var f = FlyoutModelBuilder.Build(FableState(), Now, Clock, FablePace(e));
        Assert.Equal(new FlyoutLine("With 3 sessions running: about 40m"), f.Lines[3]);
        Assert.Equal("Based on your last 9 hours of work", f.Lines[4].Text);
    }

    [Theory]
    [InlineData(false, "Based on clock time over the last 1 hour; install the hooks for a better estimate")]
    [InlineData(true, "Based on clock time over the last 1 hour")]
    public void Pace_ClockBasis(bool hooksInstalled, string expected)
    {
        var e = RunsOut with { Span = TimeSpan.FromHours(1), Basis = PaceBasis.Clock };
        var f = FlyoutModelBuilder.Build(FableState(), Now, Clock, FablePace(e), hooksInstalled);
        Assert.Equal(expected, f.Lines[3].Text);
    }

    [Fact]
    public void Pace_NothingWhenLoginExpired()
    {
        var f = FlyoutModelBuilder.Build(FableState(UsageStatus.Expired), Now, Clock, FablePace(RunsOut));
        Assert.StartsWith("Updated", f.Lines[2].Text);
    }

    [Theory]
    [InlineData(UsageStatus.Stale, "net down", "Stale: net down")]
    [InlineData(UsageStatus.Expired, null, "Login expired — run claude")]
    [InlineData(UsageStatus.NoCredentials, null, "Not signed in — run claude")]
    [InlineData(UsageStatus.Loading, null, "Loading…")]
    public void StatusTexts(UsageStatus status, string? message, string expected)
    {
        var state = new MonitorState(status, null, null, message, Creds);
        Assert.Equal(expected, FlyoutModelBuilder.Build(state, Now, Clock).StatusText);
    }

    [Theory]
    [InlineData(12, "12s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1m")]
    [InlineData(3600, "1h 0m")]
    public void AgoFormats(int seconds, string expected)
    {
        Assert.Equal(expected, AgoFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }
}
