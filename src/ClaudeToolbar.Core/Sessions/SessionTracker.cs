using ClaudeToolbar.Core.Mascot;

namespace ClaudeToolbar.Core.Sessions;

public enum SessionState { Idle, Working, NeedsAttention, Finished, Failed }

public enum SessionCue { Finished, Failed, Attention }

public sealed record SessionInfo(string Id, string Name, SessionState State, DateTimeOffset UpdatedAt, string? Message);

/// <summary>Per-session state driven by hook events. Not thread-safe: the host calls it on its UI thread.</summary>
public sealed class SessionTracker
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(12);

    private readonly Dictionary<string, SessionInfo> _sessions = new(StringComparer.Ordinal);

    /// <summary>Newest first.</summary>
    public IReadOnlyList<SessionInfo> Sessions => _sessions.Values.OrderByDescending(s => s.UpdatedAt).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();

    public SessionCue? Apply(SessionEvent e, DateTimeOffset now)
    {
        var existing = _sessions.GetValueOrDefault(e.SessionId);
        var name = NameFor(e, existing);
        switch (e.Kind)
        {
            case SessionEventKind.Ended:
                _sessions.Remove(e.SessionId);
                return null;
            case SessionEventKind.Start:
                Set(e.SessionId, name, existing?.State ?? SessionState.Idle, now, existing?.Message);
                return null;
            case SessionEventKind.Info:
                Set(e.SessionId, name, existing?.State ?? SessionState.Idle, now, existing?.Message);
                return null;
            case SessionEventKind.PromptSubmitted:
                Set(e.SessionId, name, SessionState.Working, now, null);
                return null;
            case SessionEventKind.NeedsAttention:
                var alreadyWaiting = existing?.State == SessionState.NeedsAttention;
                Set(e.SessionId, name, SessionState.NeedsAttention, now, e.Message);
                return alreadyWaiting ? null : SessionCue.Attention;
            case SessionEventKind.Stopped:
                var alreadyFinished = existing?.State == SessionState.Finished;
                Set(e.SessionId, name, SessionState.Finished, now, null);
                return alreadyFinished ? null : SessionCue.Finished;
            case SessionEventKind.Failed:
                Set(e.SessionId, name, SessionState.Failed, now, e.Detail);
                return SessionCue.Failed;
            default:
                return null;
        }
    }

    /// <summary>The user looked: finished and failed sessions go idle, waiting ones are assumed handled.</summary>
    public void Acknowledge()
    {
        foreach (var (id, s) in _sessions.ToList())
        {
            var state = s.State switch
            {
                SessionState.Finished or SessionState.Failed => SessionState.Idle,
                SessionState.NeedsAttention => SessionState.Working,
                var other => other,
            };
            if (state != s.State || s.Message is not null)
                _sessions[id] = s with { State = state, Message = null };
        }
    }

    public void Prune(DateTimeOffset now)
    {
        foreach (var (id, s) in _sessions.ToList())
            if (now - s.UpdatedAt > StaleAfter) _sessions.Remove(id);
    }

    public MascotBadge Badge => _sessions.Values
        .Select(s => s.State switch
        {
            SessionState.NeedsAttention => MascotBadge.Attention,
            SessionState.Failed => MascotBadge.Failed,
            SessionState.Finished => MascotBadge.Finished,
            SessionState.Working => MascotBadge.Working,
            _ => MascotBadge.None,
        })
        .DefaultIfEmpty(MascotBadge.None)
        .Max();

    public string? Summary
    {
        get
        {
            if (_sessions.Count == 0) return null;
            var all = Sessions;
            var parts = new List<string>();
            AddGroup(parts, all, SessionState.NeedsAttention, "needs you");
            AddGroup(parts, all, SessionState.Failed, "failed");
            AddGroup(parts, all, SessionState.Finished, "finished");
            AddGroup(parts, all, SessionState.Working, "working");
            AddGroup(parts, all, SessionState.Idle, "idle");
            var head = all.Count == 1 ? "1 session" : $"{all.Count} sessions";
            return head + " · " + string.Join(" · ", parts);
        }
    }

    private static void AddGroup(List<string> parts, IReadOnlyList<SessionInfo> all, SessionState state, string word)
    {
        var group = all.Where(s => s.State == state).ToList();
        if (group.Count == 0) return;
        var names = string.Join(", ", group.Take(2).Select(s => s.Name));
        parts.Add(all.Count == 1 ? $"{word} ({names})" : $"{group.Count} {word} ({names})");
    }

    private void Set(string id, string name, SessionState state, DateTimeOffset now, string? message) =>
        _sessions[id] = new SessionInfo(id, name, state, now, message);

    internal static string NameFor(SessionEvent e, SessionInfo? existing)
    {
        if (!string.IsNullOrWhiteSpace(e.Cwd))
        {
            var trimmed = e.Cwd.Trim().TrimEnd('/', '\\');
            var cut = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
            var leaf = cut >= 0 ? trimmed[(cut + 1)..] : trimmed;
            if (leaf.Length > 0) return leaf;
        }
        return existing?.Name ?? e.SessionId[..Math.Min(8, e.SessionId.Length)];
    }
}
