using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Pace;

/// <summary>
/// Learns how fast each limit rises per minute of Claude working, and from that how much working time is left
/// before it runs out. Not thread-safe: the host calls it on its UI thread.
/// </summary>
public sealed class PaceTracker
{
    /// <summary>The smallest rise in points the rate is measured over; below it the estimate is still learning.</summary>
    public const double MinRise = 3;

    private const int MaxPoints = 200;
    private static readonly TimeSpan ResetShift = TimeSpan.FromMinutes(10);
    private const double ResetDrop = 5;
    private static readonly TimeSpan SleepSettle = TimeSpan.FromMinutes(1);

    private readonly PaceState _state;
    private DateTimeOffset? _last;
    private DateTimeOffset? _suspendedAt;
    private int _count;

    public PaceTracker(PaceState? restored = null) => _state = restored is null ? new PaceState() : Copy(restored);

    /// <summary>How much recent working time the rate is measured over, when there is that much.</summary>
    public static TimeSpan Lookback(UsageLimit limit) =>
        limit == UsageLimit.FiveHour ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(8);

    /// <summary>Counts the time since the last call at the number of sessions that were working then.</summary>
    public void Advance(int workingSessions, DateTimeOffset now)
    {
        if (_suspendedAt is { } since)
        {
            // The sleep notice comes a moment before the machine sleeps; a tick in between must not restart the clock,
            // or the whole sleep would count once it wakes. A minute on, it has slept and woken, or the sleep was called off.
            _count = workingSessions;
            if (now - since < SleepSettle) return;
            _suspendedAt = null;
            _last = now;
            return;
        }
        if (_last is { } last)
        {
            if (now <= last)
            {
                // A clock set back adds nothing; keeping the later time avoids counting the same minutes twice.
                _count = workingSessions;
                return;
            }
            if (_count > 0)
            {
                var minutes = (now - last).TotalMinutes;
                _state.WorkMinutes += minutes * _count;
                _state.ActiveMinutes += minutes;
            }
        }
        _last = now;
        _count = workingSessions;
    }

    /// <summary>The machine is going to sleep: count up to now, then nothing until a call a minute or more later.</summary>
    public void Suspend(DateTimeOffset now)
    {
        Advance(_count, now);
        _suspendedAt = now;
    }

    public void Record(UsageSnapshot snapshot, int workingSessions)
    {
        Advance(workingSessions, snapshot.FetchedAt);
        foreach (var limit in Enum.GetValues<UsageLimit>())
        {
            if (snapshot.Window(limit) is not { } window) continue;
            var point = new PacePoint(snapshot.FetchedAt, _state.WorkMinutes, _state.ActiveMinutes, window.Utilization);
            var history = _state.Limits.GetValueOrDefault(limit);
            if (history is null || IsNewPeriod(history, window))
            {
                history = new PaceLimitHistory();
                _state.Limits[limit] = history;
            }
            history.ResetsAt = window.ResetsAt;
            if (history.Points.Count == 0 || history.Points[^1].Utilization != window.Utilization)
            {
                history.Points.Add(point);
                if (history.Points.Count > MaxPoints) history.Points.RemoveRange(0, history.Points.Count - MaxPoints);
            }
            history.Latest = point;
        }
    }

    private static bool IsNewPeriod(PaceLimitHistory history, UsageWindow window) =>
        (history.ResetsAt is { } before && window.ResetsAt is { } after && after - before > ResetShift) ||
        (history.Latest is { } latest && latest.Utilization - window.Utilization >= ResetDrop);

    public PaceEstimate Estimate(UsageLimit limit, DateTimeOffset now, int workingSessions)
    {
        if (_state.Limits.GetValueOrDefault(limit) is not { Latest: { } cur } history) return PaceEstimate.Learning;
        if (cur.Utilization >= 100) return new PaceEstimate(PaceVerdict.UsedUp, TimeSpan.Zero, null, workingSessions, TimeSpan.Zero, PaceBasis.Work);

        // Measured from the newest reading that is far enough back in working time, else from the oldest that shows a
        // rise at all. One pass without allocating: the app asks for every limit once a second.
        var lookback = Lookback(limit).TotalMinutes;
        PacePoint? oldest = null, start = null;
        foreach (var p in history.Points)
        {
            if (cur.Utilization - p.Utilization < MinRise) continue;
            oldest ??= p;
            if (cur.ActiveMinutes - p.ActiveMinutes >= lookback) start = p;
        }
        if (oldest is null) return PaceEstimate.Learning;
        start ??= oldest;

        // Where the reading last moved; it has the same value as the latest one.
        var moved = history.Points[^1];
        var dU = cur.Utilization - start.Utilization;
        var dW = cur.WorkMinutes - start.WorkMinutes;
        var dA = cur.ActiveMinutes - start.ActiveMinutes;
        var dT = (cur.At - start.At).TotalMinutes;
        var toReset = history.ResetsAt is { } resets ? (resets - now).TotalMinutes : (double?)null;

        double workLeft;
        double? available;
        double? workLeftNow = null;
        PaceBasis basis;
        double span;
        if (dW > 0)
        {
            var sessionMinutesLeft = MinutesLeft(cur.Utilization, dU, moved.WorkMinutes - start.WorkMinutes, _state.WorkMinutes - start.WorkMinutes);
            // Minutes in which several sessions worked count once each, so the answer is in the user's own time.
            workLeft = sessionMinutesLeft / Math.Max(1, dW / dA);
            if (workingSessions >= 2) workLeftNow = sessionMinutesLeft / workingSessions;
            // Only the share of the clock that usually has Claude working can be spent before the reset.
            var share = dT > 0 ? Math.Clamp(dA / dT, 0, 1) : 1;
            available = share * toReset;
            basis = PaceBasis.Work;
            span = dA;
        }
        else
        {
            if (dT <= 0) return PaceEstimate.Learning;
            var sinceStart = ((now > cur.At ? now : cur.At) - start.At).TotalMinutes;
            workLeft = MinutesLeft(cur.Utilization, dU, (moved.At - start.At).TotalMinutes, sinceStart);
            available = toReset;
            basis = PaceBasis.Clock;
            span = dT;
        }

        var verdict = available is { } a && workLeft < a ? PaceVerdict.RunsOutFirst : PaceVerdict.Lasts;
        return new PaceEstimate(verdict, Minutes(workLeft), workLeftNow is { } n ? Minutes(n) : null, workingSessions, Minutes(span), basis);
    }

    /// <summary>
    /// Time left at the rate the limit rose by <paramref name="rise"/> points. The reading moves in whole points, so the rate
    /// runs to where it last moved and the time since then is progress towards the next point; measuring to the latest
    /// reading instead would sag between points and flip the answer back and forth near the reset. A next point that is
    /// overdue lowers the rate, so a pause still slows it.
    /// </summary>
    private static double MinutesLeft(double utilization, double rise, double toMoved, double toNow)
    {
        var rate = (rise + 1) / toNow;
        if (toMoved > 0) rate = Math.Min(rate, rise / toMoved);
        var used = utilization + Math.Clamp(rate * (toNow - toMoved), 0, 1);
        return (100 - used) / rate;
    }

    public PaceState Export() => Copy(_state);

    // Whole seconds: an estimate has no use for ticks, and it keeps 6h 30m from coming out a tick short.
    private static TimeSpan Minutes(double minutes) => TimeSpan.FromSeconds(Math.Round(minutes * 60));

    private static PaceState Copy(PaceState s) => new()
    {
        Version = s.Version,
        WorkMinutes = s.WorkMinutes,
        ActiveMinutes = s.ActiveMinutes,
        Limits = s.Limits.ToDictionary(kv => kv.Key, kv => new PaceLimitHistory
        {
            ResetsAt = kv.Value.ResetsAt,
            Points = kv.Value.Points.ToList(),
            Latest = kv.Value.Latest,
        }),
    };
}
