using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Tests.Fakes;

namespace ClaudeToolbar.Core.Tests.Refresh;

public class RefreshSchedulerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DueImmediatelyAtStart()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock);
        Assert.True(s.IsDue(clock.UtcNow));
    }

    [Fact]
    public void SuccessSchedulesOneInterval()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnSuccess(null);
        Assert.Equal(T0.AddSeconds(60), s.NextDue);
        Assert.False(s.IsDue(T0.AddSeconds(59)));
        Assert.True(s.IsDue(T0.AddSeconds(60)));
    }

    [Fact]
    public void SuccessUsesUpcomingResetWhenSooner()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnSuccess(T0.AddSeconds(20));
        Assert.Equal(T0.AddSeconds(21), s.NextDue);
    }

    [Fact]
    public void PastResetIsIgnored()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnSuccess(T0.AddSeconds(-5));
        Assert.Equal(T0.AddSeconds(60), s.NextDue);
    }

    [Fact]
    public void FailureBacksOffDoublingToCap()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        var expected = new[] { 15, 30, 60, 120, 240, 300, 300 };
        foreach (var seconds in expected)
        {
            s.OnFailure(null);
            Assert.Equal(TimeSpan.FromSeconds(seconds), s.CurrentBackoff);
            Assert.Equal(clock.UtcNow.AddSeconds(seconds), s.NextDue);
        }
    }

    [Fact]
    public void RetryAfterWinsWhenLarger()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnFailure(TimeSpan.FromSeconds(100));
        Assert.Equal(T0.AddSeconds(100), s.NextDue);
        s.OnFailure(TimeSpan.FromSeconds(5));
        Assert.Equal(T0.AddSeconds(30), s.NextDue);
    }

    [Fact]
    public void SuccessResetsBackoff()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnFailure(null);
        s.OnFailure(null);
        s.OnSuccess(null);
        Assert.Equal(TimeSpan.Zero, s.CurrentBackoff);
        s.OnFailure(null);
        Assert.Equal(TimeSpan.FromSeconds(15), s.CurrentBackoff);
    }

    [Fact]
    public void PauseAndImmediate()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.Pause();
        Assert.True(s.IsPaused);
        Assert.False(s.IsDue(T0.AddDays(1)));
        s.RequestImmediate();
        Assert.False(s.IsPaused);
        Assert.True(s.IsDue(clock.UtcNow));
    }

    [Fact]
    public void IntervalChangeAppliesOnNextSuccess()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60) { IntervalSeconds = 120 };
        s.OnSuccess(null);
        Assert.Equal(T0.AddSeconds(120), s.NextDue);
    }
}
