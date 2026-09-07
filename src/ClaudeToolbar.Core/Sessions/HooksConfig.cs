using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeToolbar.Core.Sessions;

/// <summary>Adds or removes the toolbar's HTTP hooks in a Claude Code settings document. Everything else in the document is preserved.</summary>
public static class HooksConfig
{
    public static readonly IReadOnlyList<string> Events =
        ["SessionStart", "UserPromptSubmit", "Notification", "Stop", "StopFailure", "SessionEnd"];

    public const int HookTimeoutSeconds = 5;

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static string HookUrl(int port) => $"http://127.0.0.1:{port}/hook";

    public static bool IsInstalled(string json, string url)
    {
        var root = ParseObject(json);
        if (root["hooks"] is not JsonObject hooks) return false;
        return Events.All(ev => hooks[ev] is JsonArray groups && groups.Any(g => HasHandler(g, url)));
    }

    public static string Install(string json, string url)
    {
        var root = ParseObject(json);
        if (root["hooks"] is not JsonObject hooks)
        {
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }
        foreach (var ev in Events)
        {
            if (hooks[ev] is not JsonArray groups)
            {
                groups = new JsonArray();
                hooks[ev] = groups;
            }
            if (groups.Any(g => HasHandler(g, url))) continue;
            groups.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject { ["type"] = "http", ["url"] = url, ["timeout"] = HookTimeoutSeconds }),
            });
        }
        return root.ToJsonString(Pretty);
    }

    public static string Remove(string json, string url)
    {
        var root = ParseObject(json);
        if (root["hooks"] is JsonObject hooks)
        {
            foreach (var ev in Events)
            {
                if (hooks[ev] is not JsonArray groups) continue;
                foreach (var group in groups.OfType<JsonObject>().ToList())
                {
                    if (group["hooks"] is not JsonArray handlers) continue;
                    foreach (var handler in handlers.ToList())
                        if (IsOurs(handler, url)) handlers.Remove(handler);
                    if (handlers.Count == 0) groups.Remove(group);
                }
                if (groups.Count == 0) hooks.Remove(ev);
            }
            if (hooks.Count == 0) root.Remove("hooks");
        }
        return root.ToJsonString(Pretty);
    }

    private static bool HasHandler(JsonNode? group, string url) =>
        group is JsonObject g && g["hooks"] is JsonArray handlers && handlers.Any(h => IsOurs(h, url));

    private static bool IsOurs(JsonNode? handler, string url) =>
        handler is JsonObject o && StringOf(o["type"]) == "http" && StringOf(o["url"]) == url;

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    /// <summary>
    /// Blank input is an empty document. Throws <see cref="JsonException"/> when the text is not a JSON object.
    /// Strict on purpose: comments and trailing commas are rejected rather than silently rewritten away,
    /// which is what the macOS build does with the same file.
    /// </summary>
    private static JsonObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex) when (ex.GetType() != typeof(JsonException))
        {
            // The reader throws its own (non-public) JsonException subclass for malformed text;
            // normalize to the base type so callers can catch a single, documented exception.
            throw new JsonException(ex.Message, ex);
        }
        return node as JsonObject ?? throw new JsonException("The settings file is not a JSON object.");
    }
}
