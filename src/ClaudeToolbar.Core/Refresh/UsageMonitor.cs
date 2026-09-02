using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Time;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Refresh;

/// <summary>Owns the fetch loop state machine. The host calls <see cref="TickAsync"/> about once a second.</summary>
public sealed class UsageMonitor
{
    private readonly ICredentialsSource _credentials;
    private readonly IUsageClient _client;
    private readonly IClock _clock;
    private int _refreshing;
    private DateTimeOffset? _resetTriggeredFor;

    public UsageMonitor(ICredentialsSource credentials, IUsageClient client, IClock clock, RefreshScheduler scheduler)
    {
        _credentials = credentials;
        _client = client;
        _clock = clock;
        Scheduler = scheduler;
        State = MonitorState.Initial(new CredentialsState.Missing(credentials.Path));
    }

    public MonitorState State { get; private set; }

    public RefreshScheduler Scheduler { get; }

    public event Action<MonitorState>? StateChanged;

    public void RequestRefresh() => Scheduler.RequestImmediate();

    public void OnCredentialsChanged() => Scheduler.RequestImmediate();

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (State.Snapshot?.NextReset is { } reset && reset <= now && _resetTriggeredFor != reset)
        {
            _resetTriggeredFor = reset;
            Scheduler.RequestImmediate();
        }

        if (!Scheduler.IsDue(now)) return;
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;
        try
        {
            var creds = _credentials.Read();
            switch (creds)
            {
                case CredentialsState.Missing:
                    Scheduler.Pause();
                    Publish(State with { Status = UsageStatus.NoCredentials, Message = "Credentials file not found", Credentials = creds });
                    return;
                case CredentialsState.Invalid invalid:
                    Scheduler.Pause();
                    Publish(State with { Status = UsageStatus.NoCredentials, Message = invalid.Reason, Credentials = creds });
                    return;
                case CredentialsState.Expired:
                    Scheduler.Pause();
                    Publish(State with { Status = UsageStatus.Expired, Message = "Login expired", Credentials = creds });
                    return;
                case CredentialsState.Valid valid:
                    var result = await _client.FetchAsync(valid.AccessToken, cancellationToken).ConfigureAwait(false);
                    Apply(result, creds);
                    return;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void Apply(UsageResult result, CredentialsState creds)
    {
        switch (result)
        {
            case UsageResult.Ok ok:
                Scheduler.OnSuccess(ok.Snapshot.NextReset);
                Publish(new MonitorState(UsageStatus.Ok, ok.Snapshot, ok.Snapshot.FetchedAt, null, creds));
                break;
            case UsageResult.Unauthorized:
                Scheduler.Pause();
                Publish(State with { Status = UsageStatus.Expired, Message = "Token rejected", Credentials = creds });
                break;
            case UsageResult.RateLimited rl:
                Scheduler.OnFailure(rl.RetryAfter);
                Publish(State with { Status = DegradedStatus(), Message = "Rate limited", Credentials = creds });
                break;
            case UsageResult.Failed f:
                Scheduler.OnFailure(null);
                Publish(State with { Status = DegradedStatus(), Message = f.Message, Credentials = creds });
                break;
        }
    }

    private UsageStatus DegradedStatus() => State.Snapshot is null ? UsageStatus.Loading : UsageStatus.Stale;

    private void Publish(MonitorState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
