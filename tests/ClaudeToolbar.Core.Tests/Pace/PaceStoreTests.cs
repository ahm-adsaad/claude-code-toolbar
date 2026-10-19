using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Pace;

public class PaceStoreTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ct-pace-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "pace.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var state = new PaceState { WorkMinutes = 72.5, ActiveMinutes = 40.25 };
        state.Limits[UsageLimit.FiveHour] = new PaceLimitHistory
        {
            ResetsAt = T0.AddHours(5),
            Points = { new PacePoint(T0, 0, 0, 10), new PacePoint(T0.AddMinutes(10), 12.5, 10, 15.5) },
            Latest = new PacePoint(T0.AddMinutes(20), 30, 20, 15.5),
        };
        state.Limits[UsageLimit.SevenDayFable] = new PaceLimitHistory
        {
            Points = { new PacePoint(T0, 0, 0, 61) },
            Latest = new PacePoint(T0, 0, 0, 61),
        };
        var store = new PaceStore(FilePath);
        store.Save(state);

        var back = store.Load();
        Assert.NotNull(back);
        Assert.Equal((72.5, 40.25), (back.WorkMinutes, back.ActiveMinutes));
        Assert.Equal(new[] { UsageLimit.FiveHour, UsageLimit.SevenDayFable }, back.Limits.Keys.Order());
        var five = back.Limits[UsageLimit.FiveHour];
        Assert.Equal(T0.AddHours(5), five.ResetsAt);
        Assert.Equal(state.Limits[UsageLimit.FiveHour].Points, five.Points);
        Assert.Equal(state.Limits[UsageLimit.FiveHour].Latest, five.Latest);
        Assert.Null(back.Limits[UsageLimit.SevenDayFable].ResetsAt);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        Assert.Null(new PaceStore(FilePath).Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNull()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"limits\":");
        Assert.Null(new PaceStore(FilePath).Load());
    }

    [Fact]
    public void Load_FutureVersion_ReturnsNull()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"version\": 99, \"workMinutes\": 5}");
        Assert.Null(new PaceStore(FilePath).Load());
    }

    [Fact]
    public void Load_NullsInAHandEditedFile_AreDropped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"version\": 1, \"limits\": {\"FiveHour\": {\"points\": null}, \"SevenDay\": null}}");
        var back = new PaceStore(FilePath).Load();
        Assert.NotNull(back);
        Assert.Empty(back.Limits[UsageLimit.FiveHour].Points);
        Assert.False(back.Limits.ContainsKey(UsageLimit.SevenDay));
    }
}
