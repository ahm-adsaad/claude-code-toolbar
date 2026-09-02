namespace ClaudeToolbar.Core.Usage;

public abstract record UsageResult
{
    private UsageResult() { }

    public sealed record Ok(UsageSnapshot Snapshot) : UsageResult;
    public sealed record Unauthorized : UsageResult;
    public sealed record RateLimited(TimeSpan? RetryAfter) : UsageResult;
    public sealed record Failed(string Message) : UsageResult;
}
