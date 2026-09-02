using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class PercentFormatterTests
{
    [Theory]
    [InlineData(42.4, "42%")]
    [InlineData(42.5, "43%")]
    [InlineData(0, "0%")]
    [InlineData(100, "100%")]
    [InlineData(-1, "0%")]
    [InlineData(150, "100%")]
    public void FormatsRoundedAndClamped(double value, string expected)
    {
        Assert.Equal(expected, PercentFormatter.Format(value));
    }
}
