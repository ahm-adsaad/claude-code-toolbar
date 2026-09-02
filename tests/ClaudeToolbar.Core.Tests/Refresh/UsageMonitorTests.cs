using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Tests.Fakes;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Refresh;

public class UsageMonitorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(T0);
    private readonly FakeUsageClient _client = new();
    private readonly FakeCredentialsSource _creds = new();
    private readonly RefreshScheduler _scheduler;
    private readonly UsageMonitor _monitor;
    private readonly List<MonitorState> _published = new();

    public UsageMonitorTests()
    {
        _scheduler = new RefreshScheduler(_clock, 60);
        _monitor = new UsageMonitor(_creds, _client, _clock, _scheduler);
        _monitor.StateChanged += s => _published.Add(s);
    }

    private static CredentialsState.Valid ValidCreds(DateTimeOffset now) =>
        new(@"C:\fake\.credentials.json", "tok", now.AddHours(7), "max");

    private static UsageSnapshot Snapshot(DateTimeOffset now, double five = 42, double seven = 18) =>
        new(new UsageWindow(five, now.AddHours(2)), new UsageWindow(seven, now.AddDays(3)), null, null, now);

    [Fact]
    public async Task MissingCredentialsPausesWithNoCredentials()
    {
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.NoCredentials, _monitor.State.Status);
        Assert.True(_scheduler.IsPaused);
        Assert.Empty(_client.Tokens);
    }

    [Fact]
    public async Task ValidCredentialsFetchAndPublishOk()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Ok, _monitor.State.Status);
        Assert.Equal(42.0, _monitor.State.Snapshot!.FiveHour!.Utilization);
        Assert.Equal(T0, _monitor.State.LastSuccess);
        Assert.Equal(new[] { "tok" }, _client.Tokens);
        Assert.Equal(T0.AddSeconds(60), _scheduler.NextDue);
        Assert.Single(_published);
    }

    [Fact]
    public async Task NotDueMeansNoFetch()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Single(_client.Tokens);
    }

    [Fact]
    public async Task FailureWithSnapshotIsStaleAndKeepsData()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _client.Results.Enqueue(new UsageResult.Failed("net down"));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Stale, _monitor.State.Status);
        Assert.NotNull(_monitor.State.Snapshot);
        Assert.Equal("net down", _monitor.State.Message);
        Assert.Equal(TimeSpan.FromSeconds(15), _scheduler.CurrentBackoff);
    }

    [Fact]
    public async Task FailureWithoutSnapshotStaysLoading()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Failed("net down"));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Loading, _monitor.State.Status);
        Assert.Null(_monitor.State.Snapshot);
    }

    [Fact]
    public async Task UnauthorizedPausesUntilCredentialsChange()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Unauthorized());
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Expired, _monitor.State.Status);
        Assert.True(_scheduler.IsPaused);

        _monitor.OnCredentialsChanged();
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Ok, _monitor.State.Status);
    }

    [Fact]
    public async Task ExpiredCredentialsKeepPreviousSnapshot()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _creds.State = new CredentialsState.Expired(_creds.Path, T0, "max");
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Expired, _monitor.State.Status);
        Assert.NotNull(_monitor.State.Snapshot);
        Assert.True(_scheduler.IsPaused);
    }

    [Fact]
    public async Task PassedResetTriggersOneImmediateRefresh()
    {
        _creds.State = ValidCreds(T0);
        var snap = new UsageSnapshot(new UsageWindow(42, T0.AddSeconds(10)), null, null, null, T0);
        _client.Results.Enqueue(new UsageResult.Ok(snap));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(T0.AddSeconds(11), _scheduler.NextDue);

        _clock.Advance(TimeSpan.FromSeconds(11));
        _client.Results.Enqueue(new UsageResult.Ok(snap));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(2, _client.Tokens.Count);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(2, _client.Tokens.Count);
    }

    [Fact]
    public async Task ConcurrentRefreshIsIgnored()
    {
        _creds.State = ValidCreds(T0);
        _client.Pending = new TaskCompletionSource<UsageResult>();
        var first = _monitor.RefreshAsync(CancellationToken.None);
        var second = _monitor.RefreshAsync(CancellationToken.None);
        await second;
        Assert.Single(_client.Tokens);
        _client.Pending.SetResult(new UsageResult.Ok(Snapshot(T0)));
        await first;
        Assert.Equal(UsageStatus.Ok, _monitor.State.Status);
    }
}
