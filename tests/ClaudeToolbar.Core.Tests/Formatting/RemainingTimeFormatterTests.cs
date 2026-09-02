using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class RemainingTimeFormatterTests
{
    [Theory]
    [InlineData(3, 4, 10, 0, "3d 4h")]
    [InlineData(1, 0, 0, 0, "1d 0h")]
    [InlineData(0, 23, 59, 59, "23h 59m")]
    [InlineData(0, 2, 13, 5, "2h 13m")]
    [InlineData(0, 1, 0, 0, "1h 0m")]
    [InlineData(0, 0, 59, 59, "59m")]
    [InlineData(0, 0, 13, 59, "13m")]
    [InlineData(0, 0, 1, 0, "1m")]
    [InlineData(0, 0, 0, 30, "<1m")]
    [InlineData(0, 0, 0, 0, "now")]
    public void FormatsBoundaries(int d, int h, int m, int s, string expected)
    {
        var span = new TimeSpan(d, h, m, s);
        Assert.Equal(expected, RemainingTimeFormatter.Format(span));
    }

    [Fact]
    public void NegativeIsNow()
    {
        Assert.Equal("now", RemainingTimeFormatter.Format(TimeSpan.FromMinutes(-5)));
    }

    [Fact]
    public void FormatsFromInstants()
    {
        var now = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(2).AddMinutes(13);
        Assert.Equal("2h 13m", RemainingTimeFormatter.Format(reset, now));
    }
}
