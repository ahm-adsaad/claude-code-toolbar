using System.Net;
using System.Net.Http.Headers;
using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Usage;

public sealed class UsageClient : IUsageClient
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    public const string UserAgent = "claude-code/2.0.0";
    public const string BetaHeader = "oauth-2025-04-20";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly IClock _clock;

    public UsageClient(HttpClient http, IClock clock)
    {
        _http = http;
        _clock = clock;
    }

    public async Task<UsageResult> FetchAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("anthropic-beta", BetaHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new UsageResult.Unauthorized();

            if ((int)response.StatusCode == 429)
                return new UsageResult.RateLimited(ReadRetryAfter(response));

            if (!response.IsSuccessStatusCode)
                return new UsageResult.Failed($"HTTP {(int)response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            return UsageResponseParser.Parse(json, _clock.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UsageResult.Failed("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            return new UsageResult.Failed(ex.Message);
        }
        catch (IOException ex)
        {
            return new UsageResult.Failed(ex.Message);
        }
    }

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null) return null;
        if (header.Delta is { } delta) return delta;
        if (header.Date is { } date) return date - _clock.UtcNow;
        return null;
    }
}
