using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class PaceFormatterTests
{
    [Theory]
    [InlineData(0.4, "~1m")]
    [InlineData(3, "~3m")]
    [InlineData(47, "~45m")]
    [InlineData(58, "~1h")]
    [InlineData(95, "~1h 30m")]
    [InlineData(372, "~6h")]
    [InlineData(852, "~14h")]
    [InlineData(1430, "~1d")]
    [InlineData(1560, "~1d 2h")]
    public void Format_RoundsCoarserTheLongerItIs(double minutes, string expected)
    {
        Assert.Equal(expected, PaceFormatter.Format(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Format_WithoutTilde()
    {
        Assert.Equal("9h", PaceFormatter.Format(TimeSpan.FromHours(9), approximate: false));
    }

    [Theory]
    [InlineData(0.5, "1 minute")]
    [InlineData(3, "5 minutes")]
    [InlineData(30, "30 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(550, "9 hours")]
    [InlineData(47 * 60, "47 hours")]
    [InlineData(72 * 60, "3 days")]
    public void Span_InWords(double minutes, string expected)
    {
        Assert.Equal(expected, PaceFormatter.Span(TimeSpan.FromMinutes(minutes)));
    }
}
