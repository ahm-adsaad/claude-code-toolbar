namespace ClaudeToolbar.Core.Usage;

/// <summary>One rate-limit window. Utilization is 0..100. ResetsAt is null when the API gives no reset.</summary>
public sealed record UsageWindow(double Utilization, DateTimeOffset? ResetsAt);
