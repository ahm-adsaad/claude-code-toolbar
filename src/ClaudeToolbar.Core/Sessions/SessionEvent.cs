using System.Text.Json;

namespace ClaudeToolbar.Core.Sessions;

public enum SessionEventKind { Start, PromptSubmitted, NeedsAttention, Info, Stopped, Failed, Ended }

public sealed record SessionEvent(SessionEventKind Kind, string SessionId, string? Cwd, string? Message, string? Detail);

/// <summary>Turns a Claude Code hook payload into a session event. Anything we do not track returns null.</summary>
public static class HookEventParser
{
    private static readonly HashSet<string> AttentionTypes = new(StringComparer.Ordinal)
    {
        "permission_prompt", "idle_prompt", "agent_needs_input", "elicitation_dialog", "elicitation_url_dialog",
    };

    public static SessionEvent? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var name = Str(root, "hook_event_name");
            var id = Str(root, "session_id");
            if (name is null || string.IsNullOrEmpty(id)) return null;
            var cwd = Str(root, "cwd");
            switch (name)
            {
                case "SessionStart":
                    return new SessionEvent(SessionEventKind.Start, id, cwd, null, Str(root, "source"));
                case "UserPromptSubmit":
                    return new SessionEvent(SessionEventKind.PromptSubmitted, id, cwd, null, null);
                case "Notification":
                    var type = Str(root, "notification_type") ?? string.Empty;
                    var kind = AttentionTypes.Contains(type) ? SessionEventKind.NeedsAttention : SessionEventKind.Info;
                    return new SessionEvent(kind, id, cwd, Str(root, "message"), type);
                case "Stop":
                    return new SessionEvent(SessionEventKind.Stopped, id, cwd, null, null);
                case "StopFailure":
                    return new SessionEvent(SessionEventKind.Failed, id, cwd, null, Str(root, "error_type"));
                case "SessionEnd":
                    return new SessionEvent(SessionEventKind.Ended, id, cwd, null, Str(root, "reason"));
                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
