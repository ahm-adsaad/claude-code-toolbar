using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset start) => UtcNow = start;
    public DateTimeOffset UtcNow { get; set; }
    public void Advance(TimeSpan by) => UtcNow += by;
}
