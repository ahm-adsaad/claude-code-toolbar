using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Usage;

public class UsageSnapshotTests
{
    [Fact]
    public void NextResetIsEarliestNonNull()
    {
        var t = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var s = new UsageSnapshot(
            new UsageWindow(1, t.AddHours(3)),
            new UsageWindow(2, t.AddDays(2)),
            new UsageWindow(3, null),
            null,
            new UsageWindow(4, t.AddHours(1)),
            t);
        Assert.Equal(t.AddHours(1), s.NextReset);
        Assert.Equal(4, s.Windows.Count());
    }
}
