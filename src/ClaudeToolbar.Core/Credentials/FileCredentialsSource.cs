using System.Text.Json;
using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Credentials;

public sealed class FileCredentialsSource : ICredentialsSource
{
    public static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly IClock _clock;

    public FileCredentialsSource(string path, IClock clock)
    {
        Path = path;
        _clock = clock;
    }

    public string Path { get; }

    public CredentialsState Read()
    {
        if (!File.Exists(Path))
            return new CredentialsState.Missing(Path);

        string json;
        try
        {
            json = File.ReadAllText(Path);
        }
        catch (IOException ex)
        {
            return new CredentialsState.Invalid(Path, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new CredentialsState.Invalid(Path, ex.Message);
        }

        return Parse(json);
    }

    public CredentialsState Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object)
                return new CredentialsState.Invalid(Path, "claudeAiOauth section missing");

            if (!oauth.TryGetProperty("accessToken", out var tokenEl) ||
                tokenEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(tokenEl.GetString()))
                return new CredentialsState.Invalid(Path, "accessToken missing");

            if (!oauth.TryGetProperty("expiresAt", out var expEl) || expEl.ValueKind != JsonValueKind.Number)
                return new CredentialsState.Invalid(Path, "expiresAt missing");

            if (!TryReadEpochMilliseconds(expEl, out var expiresAt))
                return new CredentialsState.Invalid(Path, "expiresAt out of range");

            string? subscription = oauth.TryGetProperty("subscriptionType", out var subEl) && subEl.ValueKind == JsonValueKind.String
                ? subEl.GetString()
                : null;

            if (_clock.UtcNow >= expiresAt - ExpiryMargin)
                return new CredentialsState.Expired(Path, expiresAt, subscription);

            return new CredentialsState.Valid(Path, tokenEl.GetString()!, expiresAt, subscription);
        }
        catch (JsonException ex)
        {
            return new CredentialsState.Invalid(Path, $"Invalid JSON: {ex.Message}");
        }
    }

    private static bool TryReadEpochMilliseconds(JsonElement element, out DateTimeOffset value)
    {
        value = default;
        if (!element.TryGetInt64(out var ms))
        {
            var asDouble = element.GetDouble();
            if (double.IsNaN(asDouble) || asDouble < long.MinValue || asDouble > long.MaxValue) return false;
            ms = (long)asDouble;
        }
        try
        {
            value = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
