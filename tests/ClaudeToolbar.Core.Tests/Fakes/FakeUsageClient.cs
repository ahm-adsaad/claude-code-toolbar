using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Fakes;

public sealed class FakeUsageClient : IUsageClient
{
    public Queue<UsageResult> Results { get; } = new();
    public List<string> Tokens { get; } = new();
    public TaskCompletionSource<UsageResult>? Pending { get; set; }
    public Exception? Throw { get; set; }

    public Task<UsageResult> FetchAsync(string accessToken, CancellationToken cancellationToken)
    {
        Tokens.Add(accessToken);
        if (Throw is not null) throw Throw;
        if (Pending is not null) return Pending.Task;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new UsageResult.Failed("no canned result"));
    }
}
