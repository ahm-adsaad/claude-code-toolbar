using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Refresh;

/// <summary>Decides when the next usage fetch is due. Thread-safe: the monitor updates it from the fetch continuation while the host reads it from the UI thread.</summary>
public sealed class RefreshScheduler
{
    public static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan ResetGrace = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private readonly IClock _clock;

    private int _intervalSeconds;
    private DateTimeOffset? _nextDue;
    private TimeSpan _backoff = TimeSpan.Zero;

    public RefreshScheduler(IClock clock, int intervalSeconds = 60)
    {
        _clock = clock;
        _intervalSeconds = intervalSeconds;
        _nextDue = clock.UtcNow;
    }

    public int IntervalSeconds
    {
        get { lock (_gate) return _intervalSeconds; }
        set { lock (_gate) _intervalSeconds = value; }
    }

    public DateTimeOffset? NextDue
    {
        get { lock (_gate) return _nextDue; }
    }

    public bool IsPaused => NextDue is null;

    public TimeSpan CurrentBackoff
    {
        get { lock (_gate) return _backoff; }
    }

    public bool IsDue(DateTimeOffset now)
    {
        lock (_gate) return _nextDue is { } due && now >= due;
    }

    public void RequestImmediate()
    {
        lock (_gate) _nextDue = _clock.UtcNow;
    }

    public void Pause()
    {
        lock (_gate) _nextDue = null;
    }

    public void OnSuccess(DateTimeOffset? nextReset)
    {
        lock (_gate)
        {
            _backoff = TimeSpan.Zero;
            var now = _clock.UtcNow;
            var due = now + TimeSpan.FromSeconds(_intervalSeconds);
            if (nextReset is { } reset)
            {
                var afterReset = reset + ResetGrace;
                if (afterReset > now && afterReset < due) due = afterReset;
            }
            _nextDue = due;
        }
    }

    public void OnFailure(TimeSpan? retryAfter)
    {
        lock (_gate)
        {
            _backoff = _backoff == TimeSpan.Zero
                ? InitialBackoff
                : TimeSpan.FromTicks(Math.Min((_backoff * 2).Ticks, MaxBackoff.Ticks));
            var delay = retryAfter is { } ra && ra > _backoff ? ra : _backoff;
            _nextDue = _clock.UtcNow + delay;
        }
    }
}
