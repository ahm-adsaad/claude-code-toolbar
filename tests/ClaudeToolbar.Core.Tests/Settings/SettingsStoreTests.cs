using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ct-settings-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaultsWithoutCreatingIt()
    {
        var store = new SettingsStore(FilePath);
        var s = store.Load();
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var s = AppSettings.CreateDefault();
        s.Appearance.Background = "#80FF0000";
        s.Rows.ShowSevenDayOpus = true;
        s.Behavior.RefreshIntervalSeconds = 120;
        store.Save(s);
        var back = store.Load();
        Assert.Equal("#80FF0000", back.Appearance.Background);
        Assert.True(back.Rows.ShowSevenDayOpus);
        Assert.Equal(120, back.Behavior.RefreshIntervalSeconds);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void UnknownFieldsAreIgnoredAndMissingFieldsDefault()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "version": 1, "future": { "x": 1 }, "behavior": { "trayGapPx": 12 } }""");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(12, s.Behavior.TrayGapPx);
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.Equal("dark", s.Appearance.Preset);
    }

    [Fact]
    public void CorruptFileIsBackedUpAndReplaced()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ this is not json");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.Contains("\"version\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var a = AppSettings.CreateDefault();
        var b = a.Clone();
        b.Appearance.FontSize = 13;
        Assert.Equal(11, a.Appearance.FontSize);
    }
}
