using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Pace;

public enum PaceVerdict { Learning, Lasts, RunsOutFirst, UsedUp }

/// <summary>What the rate was measured against: time Claude spent working, or the clock when no working time was recorded.</summary>
public enum PaceBasis { Work, Clock }

/// <param name="WorkLeft">How long the limit lasts at the usual pace, in working time.</param>
/// <param name="WorkLeftNow">How long it lasts with the sessions working right now; only set for two or more.</param>
/// <param name="Span">How much working (or clock) time the rate was measured over.</param>
public sealed record PaceEstimate(PaceVerdict Verdict, TimeSpan? WorkLeft, TimeSpan? WorkLeftNow, int SessionsNow, TimeSpan Span, PaceBasis Basis)
{
    public static PaceEstimate Learning { get; } = new(PaceVerdict.Learning, null, null, 0, TimeSpan.Zero, PaceBasis.Work);
}

/// <summary>A reading of one limit, with the running totals of working time it was taken at.</summary>
public sealed record PacePoint(DateTimeOffset At, double WorkMinutes, double ActiveMinutes, double Utilization);

/// <summary>One limit's readings since its last reset. Settable for JSON.</summary>
public sealed class PaceLimitHistory
{
    public DateTimeOffset? ResetsAt { get; set; }
    /// <summary>The readings where utilization changed, oldest first.</summary>
    public List<PacePoint> Points { get; set; } = new();
    public PacePoint? Latest { get; set; }
}

/// <summary>Everything the tracker keeps across restarts. Settable for JSON.</summary>
public sealed class PaceState
{
    public int Version { get; set; } = 1;
    /// <summary>Session-minutes: a minute with two sessions working counts twice.</summary>
    public double WorkMinutes { get; set; }
    /// <summary>Minutes in which at least one session was working.</summary>
    public double ActiveMinutes { get; set; }
    public Dictionary<UsageLimit, PaceLimitHistory> Limits { get; set; } = new();
}
