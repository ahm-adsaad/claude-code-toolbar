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
                ReadWindow(root, "seven_day_fable") ?? ReadScopedWeekly(root, "Fable"),
                fetchedAt);
            return new UsageResult.Ok(snapshot);
        }
    }

    /// <summary>
    /// Newer models have no <c>seven_day_&lt;model&gt;</c> field: their weekly limit is a <c>weekly_scoped</c> entry in
    /// <c>limits</c>, named by <c>scope.model.display_name</c>.
    /// </summary>
    private static UsageWindow? ReadScopedWeekly(JsonElement root, string model)
    {
        if (!root.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var limit in limits.EnumerateArray())
        {
            if (Str(limit, "kind") != "weekly_scoped") continue;
            if (!string.Equals(Str(Obj(Obj(limit, "scope"), "model"), "display_name"), model, StringComparison.OrdinalIgnoreCase)) continue;
            if (!limit.TryGetProperty("percent", out var p) || p.ValueKind != JsonValueKind.Number || !p.TryGetDouble(out var raw)) continue;
            return new UsageWindow(Math.Clamp(raw, 0, 100), ReadResetsAt(limit));
        }
        return null;
    }

    private static JsonElement? Obj(JsonElement? el, string name) =>
        el is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static string? Str(JsonElement? el, string name) =>
        el is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static UsageWindow? ReadWindow(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        if (!el.TryGetProperty("utilization", out var u) || u.ValueKind != JsonValueKind.Number || !u.TryGetDouble(out var raw))
            return null;

        return new UsageWindow(Math.Clamp(raw, 0, 100), ReadResetsAt(el));
    }

    private static DateTimeOffset? ReadResetsAt(JsonElement el) =>
        el.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
