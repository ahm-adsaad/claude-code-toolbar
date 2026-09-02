using System.Globalization;
using System.Text.Json;

namespace ClaudeToolbar.Core.Usage;

public static class UsageResponseParser
{
    public static UsageResult Parse(string json, DateTimeOffset fetchedAt)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new UsageResult.Failed("Empty response");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new UsageResult.Failed($"Invalid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new UsageResult.Failed("Response is not a JSON object");

            var snapshot = new UsageSnapshot(
                ReadWindow(root, "five_hour"),
                ReadWindow(root, "seven_day"),
                ReadWindow(root, "seven_day_opus"),
                ReadWindow(root, "seven_day_sonnet"),
                fetchedAt);
            return new UsageResult.Ok(snapshot);
        }
    }

    private static UsageWindow? ReadWindow(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        if (!el.TryGetProperty("utilization", out var u) || u.ValueKind != JsonValueKind.Number)
            return null;

        DateTimeOffset? resetsAt = null;
        if (el.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            resetsAt = parsed;
        }

        return new UsageWindow(Math.Clamp(u.GetDouble(), 0, 100), resetsAt);
    }
}
