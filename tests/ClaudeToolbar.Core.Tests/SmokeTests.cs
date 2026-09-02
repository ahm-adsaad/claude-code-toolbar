namespace ClaudeToolbar.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        var assembly = typeof(SmokeTests).Assembly.GetReferencedAssemblies()
            .Single(a => a.Name == "ClaudeToolbar.Core");
        Assert.NotNull(assembly);
    }
}
