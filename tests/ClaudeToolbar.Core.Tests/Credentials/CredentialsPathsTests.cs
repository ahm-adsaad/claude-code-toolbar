using ClaudeToolbar.Core.Credentials;

namespace ClaudeToolbar.Core.Tests.Credentials;

public class CredentialsPathsTests
{
    [Fact]
    public void DefaultsToUserProfileClaudeDir()
    {
        var path = CredentialsPaths.Resolve(null, @"C:\Users\me");
        Assert.Equal(Path.Combine(@"C:\Users\me", ".claude", ".credentials.json"), path);
    }

    [Fact]
    public void EnvOverrideWins()
    {
        var path = CredentialsPaths.Resolve(@"D:\cfg", @"C:\Users\me");
        Assert.Equal(Path.Combine(@"D:\cfg", ".credentials.json"), path);
    }

    [Fact]
    public void BlankOverrideIsIgnored()
    {
        var path = CredentialsPaths.Resolve("   ", @"C:\Users\me");
        Assert.Equal(Path.Combine(@"C:\Users\me", ".claude", ".credentials.json"), path);
    }
}
