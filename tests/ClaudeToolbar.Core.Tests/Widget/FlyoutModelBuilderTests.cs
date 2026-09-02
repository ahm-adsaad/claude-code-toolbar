using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Widget;

public class FlyoutModelBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly CredentialsState Creds = new CredentialsState.Valid("p", "t", Now.AddHours(7), "max");
    private static string Clock(DateTimeOffset t) => t.ToString("HH:mm");

    [Fact]
    public void ListsWindowsUpdatedAndStatus()
    {
        var snap = new UsageSnapshot(
            new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
            new UsageWindow(18, Now.AddDays(3).AddHours(4)),
            new UsageWindow(5, Now.AddDays(3)),
            null,
            Now.AddSeconds(-12));
        var state = new MonitorState(UsageStatus.Ok, snap, Now.AddSeconds(-12), null, Creds);
        var f = FlyoutModelBuilder.Build(state, Now, Clock);
        Assert.Equal("Session 42% · resets in 2h 13m · at 12:13", f.Lines[0]);
        Assert.Equal("Weekly 18% · resets in 3d 4h · at 14:00", f.Lines[1]);
        Assert.Equal("Weekly Opus 5% · resets in 3d 0h · at 10:00", f.Lines[2]);
        Assert.Equal("Updated 12s ago", f.Lines[3]);
        Assert.Equal(4, f.Lines.Count);
        Assert.Equal("OK", f.StatusText);
    }

    [Theory]
    [InlineData(UsageStatus.Stale, "net down", "Stale: net down")]
    [InlineData(UsageStatus.Expired, null, "Login expired — run claude")]
    [InlineData(UsageStatus.NoCredentials, null, "Not signed in — run claude")]
    [InlineData(UsageStatus.Loading, null, "Loading…")]
    public void StatusTexts(UsageStatus status, string? message, string expected)
    {
        var state = new MonitorState(status, null, null, message, Creds);
        Assert.Equal(expected, FlyoutModelBuilder.Build(state, Now, Clock).StatusText);
    }

    [Theory]
    [InlineData(12, "12s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1m")]
    [InlineData(3600, "1h 0m")]
    public void AgoFormats(int seconds, string expected)
    {
        Assert.Equal(expected, AgoFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }
}
