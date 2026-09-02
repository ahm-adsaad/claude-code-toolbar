using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Tests.Fakes;

namespace ClaudeToolbar.Core.Tests.Credentials;

public class FileCredentialsSourceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ct-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeClock _clock = new(Now);

    public FileCredentialsSourceTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string json)
    {
        var path = Path.Combine(_dir, ".credentials.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Payload(long expiresAtMs, string token = "sk-ant-oat01-secret") =>
        $$"""{ "claudeAiOauth": { "accessToken": "{{token}}", "refreshToken": "sk-ant-ort01-x", "expiresAt": {{expiresAtMs}}, "scopes": ["user:inference","user:profile"], "subscriptionType": "max" } }""";

    [Fact]
    public void MissingFile()
    {
        var src = new FileCredentialsSource(Path.Combine(_dir, "nope.json"), _clock);
        var state = Assert.IsType<CredentialsState.Missing>(src.Read());
        Assert.EndsWith("nope.json", state.Path);
    }

    [Fact]
    public void ValidToken()
    {
        var path = Write(Payload(Now.AddHours(7).ToUnixTimeMilliseconds()));
        var state = Assert.IsType<CredentialsState.Valid>(new FileCredentialsSource(path, _clock).Read());
        Assert.Equal("sk-ant-oat01-secret", state.AccessToken);
        Assert.Equal("max", state.SubscriptionType);
        Assert.Equal(Now.AddHours(7), state.ExpiresAt);
    }

    [Fact]
    public void ExpiredToken()
    {
        var path = Write(Payload(Now.AddMinutes(-1).ToUnixTimeMilliseconds()));
        var state = Assert.IsType<CredentialsState.Expired>(new FileCredentialsSource(path, _clock).Read());
        Assert.Equal("max", state.SubscriptionType);
    }

    [Fact]
    public void TokenInsideSafetyMarginIsExpired()
    {
        var path = Write(Payload(Now.AddSeconds(30).ToUnixTimeMilliseconds()));
        Assert.IsType<CredentialsState.Expired>(new FileCredentialsSource(path, _clock).Read());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{}")]
    [InlineData("""{ "claudeAiOauth": { "expiresAt": 1 } }""")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "x" } }""")]
    public void InvalidShapes(string json)
    {
        var path = Write(json);
        Assert.IsType<CredentialsState.Invalid>(new FileCredentialsSource(path, _clock).Read());
    }

    [Fact]
    public void ToStringNeverContainsToken()
    {
        var path = Write(Payload(Now.AddHours(7).ToUnixTimeMilliseconds(), token: "SUPERSECRET"));
        var state = new FileCredentialsSource(path, _clock).Read();
        Assert.DoesNotContain("SUPERSECRET", state.ToString());
    }
}
