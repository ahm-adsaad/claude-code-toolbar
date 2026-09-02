using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class BarLevelResolverTests
{
    [Theory]
    [InlineData(0, BarLevel.Ok)]
    [InlineData(69.9, BarLevel.Ok)]
    [InlineData(70, BarLevel.Warn)]
    [InlineData(89.9, BarLevel.Warn)]
    [InlineData(90, BarLevel.Crit)]
    [InlineData(100, BarLevel.Crit)]
    public void ResolvesAgainstThresholds(double utilization, BarLevel expected)
    {
        Assert.Equal(expected, BarLevelResolver.Resolve(utilization, 70, 90));
    }
}
