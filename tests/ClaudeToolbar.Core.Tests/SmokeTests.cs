using System.Reflection;

namespace ClaudeToolbar.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        var assembly = Assembly.Load("ClaudeToolbar.Core");
        Assert.Equal("ClaudeToolbar.Core", assembly.GetName().Name);
    }
}
