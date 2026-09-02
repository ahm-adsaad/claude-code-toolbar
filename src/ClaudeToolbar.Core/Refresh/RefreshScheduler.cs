using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Refresh;

/// <summary>Decides when the next usage fetch is due. Not thread-safe; call from one thread.</summary>
public sealed class RefreshScheduler
{
    public static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan ResetGrace = TimeSpan.FromSeconds(1);

    private readonly IClock _clock;

    public RefreshScheduler(IClock clock, int intervalSeconds = 60)
    {
        _clock = clock;
        IntervalSeconds = intervalSeconds;
        NextDue = clock.UtcNow;
    }

    public int IntervalSeconds { get; set; }

    public DateTimeOffset? NextDue { get; private set; }

    public bool IsPaused => NextDue is null;

    public TimeSpan CurrentBackoff { get; private set; } = TimeSpan.Zero;

    public bool IsDue(DateTimeOffset now) => NextDue is { } due && now >= due;

    public void RequestImmediate() => NextDue = _clock.UtcNow;

    public void Pause() => NextDue = null;

    public void OnSuccess(DateTimeOffset? nextReset)
    {
        CurrentBackoff = TimeSpan.Zero;
        var now = _clock.UtcNow;
        var due = now + TimeSpan.FromSeconds(IntervalSeconds);
        if (nextReset is { } reset)
        {
            var afterReset = reset + ResetGrace;
            if (afterReset > now && afterReset < due) due = afterReset;
        }
        NextDue = due;
    }

    public void OnFailure(TimeSpan? retryAfter)
    {
        CurrentBackoff = CurrentBackoff == TimeSpan.Zero
            ? InitialBackoff
            : TimeSpan.FromTicks(Math.Min((CurrentBackoff * 2).Ticks, MaxBackoff.Ticks));
        var delay = retryAfter is { } ra && ra > CurrentBackoff ? ra : CurrentBackoff;
        NextDue = _clock.UtcNow + delay;
    }
}
