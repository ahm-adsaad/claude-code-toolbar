using System.Net;
using System.Net.Http.Headers;
using ClaudeToolbar.Core.Tests.Fakes;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Usage;

public class UsageClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Respond(request));
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private static (UsageClient client, StubHandler handler) Make()
    {
        var handler = new StubHandler();
        return (new UsageClient(new HttpClient(handler), new FakeClock(Now)), handler);
    }

    [Fact]
    public async Task SendsRequiredHeaders()
    {
        var (client, handler) = Make();
        await client.FetchAsync("tok", CancellationToken.None);
        var req = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal(UsageClient.Endpoint, req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("tok", req.Headers.Authorization.Parameter);
        Assert.Equal("oauth-2025-04-20", req.Headers.GetValues("anthropic-beta").Single());
        Assert.Equal("claude-code/2.0.0", req.Headers.GetValues("User-Agent").Single());
        Assert.Contains(req.Headers.Accept, a => a.MediaType == "application/json");
    }

    [Fact]
    public async Task OkParsesBody()
    {
        var (client, handler) = Make();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "five_hour": { "utilization": 42, "resets_at": "2026-09-03T14:00:00Z" } }""")
        };
        var ok = Assert.IsType<UsageResult.Ok>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal(42.0, ok.Snapshot.FiveHour!.Utilization);
        Assert.Equal(Now, ok.Snapshot.FetchedAt);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthFailuresAreUnauthorized(HttpStatusCode code)
    {
        var (client, handler) = Make();
        handler.Respond = _ => new HttpResponseMessage(code);
        Assert.IsType<UsageResult.Unauthorized>(await client.FetchAsync("tok", CancellationToken.None));
    }

    [Fact]
    public async Task RateLimitedCarriesRetryAfter()
    {
        var (client, handler) = Make();
        handler.Respond = _ =>
        {
            var r = new HttpResponseMessage((HttpStatusCode)429);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return r;
        };
        var rl = Assert.IsType<UsageResult.RateLimited>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(120), rl.RetryAfter);
    }

    [Fact]
    public async Task ServerErrorFails()
    {
        var (client, handler) = Make();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var f = Assert.IsType<UsageResult.Failed>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal("HTTP 500", f.Message);
    }

    [Fact]
    public async Task NetworkErrorFails()
    {
        var (client, handler) = Make();
        handler.Respond = _ => throw new HttpRequestException("boom");
        var f = Assert.IsType<UsageResult.Failed>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal("boom", f.Message);
    }
}
