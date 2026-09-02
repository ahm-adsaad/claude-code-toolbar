using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Refresh;

public enum UsageStatus
{
    Loading,
    Ok,
    Stale,
    Expired,
    NoCredentials,
}

public sealed record MonitorState(
    UsageStatus Status,
    UsageSnapshot? Snapshot,
    DateTimeOffset? LastSuccess,
    string? Message,
    CredentialsState Credentials)
{
    public static MonitorState Initial(CredentialsState credentials) =>
        new(UsageStatus.Loading, null, null, null, credentials);
}
