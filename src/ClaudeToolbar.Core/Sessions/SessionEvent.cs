using System.Text.Json;

namespace ClaudeToolbar.Core.Sessions;

public enum SessionEventKind { Start, PromptSubmitted, NeedsAttention, Info, Stopped, Failed, Ended }

/// <summary>
/// <paramref name="RunningAgents"/> is only ever non-zero on a Stopped event: the number of background
/// agents the turn left running, taken from the Stop payload's <c>background_tasks</c>.
/// </summary>
public sealed record SessionEvent(SessionEventKind Kind, string SessionId, string? Cwd, string? Message, string? Detail, int RunningAgents = 0);

/// <summary>Turns a Claude Code hook payload into a session event. Anything we do not track returns null.</summary>
public static class HookEventParser
{
    // Not "idle_prompt": Claude Code sends it a minute after every turn ends ("Claude is waiting for your input"),
    // which is the finished cue again with a false "needs you" attached.
    private static readonly HashSet<string> AttentionTypes = new(StringComparer.Ordinal)
    {
        "permission_prompt", "worker_permission_prompt", "agent_needs_input", "elicitation_dialog", "elicitation_url_dialog",
    };

    /// <summary>Background task types that are agents whose results the session is still waiting for. A dev server is not one.</summary>
    private static readonly HashSet<string> AgentTaskTypes = new(StringComparer.Ordinal)
    {
        "subagent", "local_agent", "remote_agent", "in_process_teammate",
    };

    private static readonly HashSet<string> RunningStatuses = new(StringComparer.Ordinal) { "running", "pending" };

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
                    return new SessionEvent(SessionEventKind.Stopped, id, cwd, null, null, RunningAgents(root));
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

    /// <summary>
    /// Stop fires when the main agent's turn ends, even while agents it launched in the background are still
    /// working; the payload lists them. Counting them here is what lets the tracker hold "finished" back.
    /// </summary>
    private static int RunningAgents(JsonElement root)
    {
        if (!root.TryGetProperty("background_tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array) return 0;
        var count = 0;
        foreach (var task in tasks.EnumerateArray())
        {
            if (task.ValueKind != JsonValueKind.Object) continue;
            if (Str(task, "type") is { } type && AgentTaskTypes.Contains(type)
                && Str(task, "status") is { } status && RunningStatuses.Contains(status))
                count++;
        }
        return count;
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
