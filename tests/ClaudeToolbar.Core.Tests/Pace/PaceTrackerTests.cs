using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Pace;

public class PaceTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Reset = T0.AddHours(5);

    private static UsageSnapshot Five(double utilization, DateTimeOffset at, DateTimeOffset? resets = null) =>
        new(new UsageWindow(utilization, resets ?? Reset), null, null, null, null, at);

    /// <summary>One 5-hour reading every 10 minutes from <paramref name="from"/>, with <paramref name="sessions"/> working throughout.</summary>
    private static void Run(PaceTracker t, int sessions, DateTimeOffset from, params double[] readings)
    {
        for (var i = 0; i < readings.Length; i++)
            t.Record(Five(readings[i], from.AddMinutes(10 * i)), sessions);
    }

    private static PaceTracker Steady()
    {
        var t = new PaceTracker();
        Run(t, 1, T0, 10, 15, 20, 25, 30, 35, 40);
        return t;
    }

    [Fact]
    public void Learning_BelowThreePoints()
    {
        var t = new PaceTracker();
        Run(t, 1, T0, 10, 11, 11, 12);
        Assert.Equal(PaceVerdict.Learning, t.Estimate(UsageLimit.FiveHour, T0.AddMinutes(30), 1).Verdict);
    }

    [Fact]
    public void SteadyPace_RunsOutFirst()
    {
        var e = Steady().Estimate(UsageLimit.FiveHour, T0.AddMinutes(60), 1);
        Assert.Equal(PaceVerdict.RunsOutFirst, e.Verdict);
        Assert.Equal(TimeSpan.FromHours(2), e.WorkLeft);
        Assert.Equal(PaceBasis.Work, e.Basis);
        Assert.Equal(TimeSpan.FromMinutes(30), e.Span);
    }

    [Fact]
    public void SlowPace_Lasts()
    {
        var t = new PaceTracker();
        Run(t, 1, T0, 10, 12, 14, 16, 18, 20, 22);
        var e = t.Estimate(UsageLimit.FiveHour, T0.AddMinutes(60), 1);
        Assert.Equal(PaceVerdict.Lasts, e.Verdict);
        Assert.Equal(new TimeSpan(6, 30, 0), e.WorkLeft);
    }

    [Fact]
    public void IdleTime_DoesNotChangeWorkLeft()
    {
        var t = new PaceTracker();
        Run(t, 1, T0, 10, 15, 20, 25);
        t.Advance(0, T0.AddMinutes(30));
        Run(t, 0, T0.AddMinutes(40), Enumerable.Repeat(25.0, 12).ToArray());
        t.Advance(1, T0.AddMinutes(150));
        Run(t, 1, T0.AddMinutes(160), 30, 35, 40);
        Assert.Equal(TimeSpan.FromHours(2), t.Estimate(UsageLimit.FiveHour, T0.AddMinutes(180), 1).WorkLeft);
    }

    [Fact]
    public void ParallelSessions()
    {
        var t = new PaceTracker();
        Run(t, 2, T0, 10, 20, 30, 40);
        var now = T0.AddMinutes(30);
        Assert.Equal(TimeSpan.FromHours(1), t.Estimate(UsageLimit.FiveHour, now, 2).WorkLeft);
        var three = t.Estimate(UsageLimit.FiveHour, now, 3);
        Assert.Equal(TimeSpan.FromMinutes(40), three.WorkLeftNow);
        Assert.Equal(3, three.SessionsNow);
        Assert.Null(t.Estimate(UsageLimit.FiveHour, now, 1).WorkLeftNow);
    }

    [Fact]
    public void Reset_StartsOver()
    {
        var t = Steady();
        t.Record(Five(2, T0.AddMinutes(70), Reset.AddHours(5)), 1);
        Assert.Equal(PaceVerdict.Learning, t.Estimate(UsageLimit.FiveHour, T0.AddMinutes(70), 1).Verdict);
    }

    [Fact]
    public void SmallResetShift_KeepsHistory()
    {
        var t = Steady();
        var now = T0.AddMinutes(60);
        var before = t.Estimate(UsageLimit.FiveHour, now, 1);
        t.Record(Five(40, now, Reset.AddSeconds(30)), 1);
        Assert.Equal(before, t.Estimate(UsageLimit.FiveHour, now, 1));
    }

    [Fact]
    public void SmallDip_KeepsHistory()
    {
        var t = Steady();
        t.Record(Five(39, T0.AddMinutes(70)), 1);
        Assert.NotEqual(PaceVerdict.Learning, t.Estimate(UsageLimit.FiveHour, T0.AddMinutes(70), 1).Verdict);
    }

    [Fact]
    public void ClockFallback_WhenNoWorkRecorded()
    {
        var t = new PaceTracker();
        Run(t, 0, T0, 10, 15, 20, 25, 30, 35, 40);
        var e = t.Estimate(UsageLimit.FiveHour, T0.AddMinutes(60), 0);
        Assert.Equal(PaceVerdict.RunsOutFirst, e.Verdict);
        Assert.Equal(TimeSpan.FromHours(2), e.WorkLeft);
        Assert.Equal(PaceBasis.Clock, e.Basis);
        Assert.Equal(TimeSpan.FromHours(1), e.Span);
    }

    [Fact]
    public void UsedUp_AtHundred()
    {
        var t = new PaceTracker();
        t.Record(Five(100, T0), 1);
        Assert.Equal(PaceVerdict.UsedUp, t.Estimate(UsageLimit.FiveHour, T0, 1).Verdict);
    }

    [Fact]
    public void Suspend_SkipsTheGap()
    {
        var t = new PaceTracker();
        t.Advance(1, T0);
        t.Suspend(T0.AddMinutes(10));
        t.Advance(1, T0.AddHours(8));
        Assert.Equal(10, t.Export().WorkMinutes);
    }

    [Fact]
    public void ClockGoingBackwards_AddsNothing()
    {
        var t = new PaceTracker();
        t.Advance(1, T0);
        t.Advance(1, T0.AddMinutes(10));
        t.Advance(1, T0.AddMinutes(5));
        Assert.Equal(10, t.Export().WorkMinutes);
    }

    [Fact]
    public void LimitAppearingLater_IsIndependent()
    {
        var t = new PaceTracker();
        Run(t, 1, T0, 10, 15, 20);
        var fable = new UsageWindow(30, T0.AddDays(3));
        t.Record(Five(25, T0.AddMinutes(30)) with { SevenDayFable = fable }, 1);
        Assert.Equal(PaceVerdict.Learning, t.Estimate(UsageLimit.SevenDayFable, T0.AddMinutes(30), 1).Verdict);
        Assert.Equal(PaceVerdict.Learning, t.Estimate(UsageLimit.SevenDayOpus, T0.AddMinutes(30), 1).Verdict);
    }

    [Fact]
    public void ExportAndRestore_GiveTheSameEstimate()
    {
        var t = Steady();
        var restored = new PaceTracker(t.Export());
        var now = T0.AddMinutes(60);
        Assert.Equal(t.Estimate(UsageLimit.FiveHour, now, 1), restored.Estimate(UsageLimit.FiveHour, now, 1));
    }
}
