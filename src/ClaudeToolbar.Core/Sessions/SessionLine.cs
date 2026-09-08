namespace ClaudeToolbar.Core.Sessions;

/// <summary>One flyout or popover line per session: "api · needs you · VS Code · 2 min".</summary>
public static class SessionLine
{
    public static string Text(SessionInfo s, DateTimeOffset now)
    {
        var parts = new List<string> { s.Name, StateWord(s.State) };
        if (s.Host is { } host) parts.Add(host.Name);
        parts.Add(Age(now - s.UpdatedAt));
        return string.Join(" · ", parts);
    }

    public static string StateWord(SessionState state) => state switch
    {
        SessionState.NeedsAttention => "needs you",
        SessionState.Failed => "failed",
        SessionState.Finished => "finished",
        SessionState.Working => "working",
        _ => "idle",
    };

    public static string Age(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1)) return "now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min";
        return $"{(int)age.TotalHours} h";
    }
}
