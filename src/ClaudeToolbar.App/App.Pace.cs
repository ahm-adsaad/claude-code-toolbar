using ClaudeToolbar.App.Services;
using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.App;

public partial class App
{
    private static readonly TimeSpan PaceSaveEvery = TimeSpan.FromMinutes(5);

    private readonly PaceStore _paceStore = new(PaceStore.DefaultPath());
    private PaceTracker? _pace;
    private DateTimeOffset? _paceRecordedAt;
    private DateTime _paceSavedAt = DateTime.MinValue;

    private void StartPace()
    {
        var restored = _paceStore.Load();
        _pace = new PaceTracker(restored);
        Log.Info(restored is null ? "Pace: starting fresh" : $"Pace: restored {restored.Limits.Count} limits");
        AdvancePace();
    }

    /// <summary>Counts working time up to now. Called whenever the number of working sessions may have changed, and every tick.</summary>
    private void AdvancePace() => _pace?.Advance(_sessions.WorkingCount, DateTimeOffset.UtcNow);

    private void RecordPace(MonitorState state)
    {
        if (_pace is null || state.Status != UsageStatus.Ok || state.Snapshot is not { } snapshot || snapshot.FetchedAt == _paceRecordedAt) return;
        _paceRecordedAt = snapshot.FetchedAt;
        _pace.Record(snapshot, _sessions.WorkingCount);
        SavePace();
    }

    /// <summary>The time is taken when Windows announces the sleep: the callback may only run after the machine wakes.</summary>
    private void SuspendPace(DateTimeOffset at)
    {
        _pace?.Suspend(at);
        SavePace(force: true);
    }

    private void SavePace(bool force = false)
    {
        if (_pace is null || (!force && DateTime.UtcNow - _paceSavedAt < PaceSaveEvery)) return;
        _paceSavedAt = DateTime.UtcNow;
        TrySafe(() => _paceStore.Save(_pace.Export()), "save pace");
    }

    /// <summary>An estimate for each limit the current snapshot reports.</summary>
    public IReadOnlyDictionary<UsageLimit, PaceEstimate> PaceEstimates(DateTimeOffset now)
    {
        var estimates = new Dictionary<UsageLimit, PaceEstimate>();
        if (_pace is null || _monitor?.State.Snapshot is not { } snapshot) return estimates;
        var working = _sessions.WorkingCount;
        foreach (var limit in Enum.GetValues<UsageLimit>())
            if (snapshot.Window(limit) is not null) estimates[limit] = _pace.Estimate(limit, now, working);
        return estimates;
    }
}
