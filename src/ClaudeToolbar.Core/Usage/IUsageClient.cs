namespace ClaudeToolbar.Core.Usage;

public interface IUsageClient
{
    Task<UsageResult> FetchAsync(string accessToken, CancellationToken cancellationToken);
}
