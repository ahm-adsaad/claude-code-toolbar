namespace ClaudeToolbar.Core.Sessions;

/// <summary>
/// The window-owning process that hosts a Claude Code session: a terminal, an editor or the desktop app.
/// <paramref name="ClientPid"/> is the Claude Code process the hooks come from, with its start time so a
/// reused pid is not mistaken for it; the Windows app reads that process's console at click time to find
/// the terminal tab. Zero when unknown.
/// </summary>
public sealed record SessionHost(int Pid, string Name, DateTimeOffset ResolvedAt, int ClientPid = 0, DateTimeOffset ClientStart = default);

/// <summary>One process as the platform reports it: enough to walk parents and tell hosts apart.</summary>
public sealed record ProcessRecord(int Pid, int ParentPid, string Name, DateTimeOffset StartTime, bool HasWindow);

/// <summary>Known is true when the name came from the catalog; false when an unknown windowed ancestor was used.</summary>
public sealed record HostChainResult(int Pid, string DisplayName, bool Known);

/// <summary>
/// Per-platform naming: <see cref="DisplayName"/> maps a process name (Windows: lower-case executable
/// base name; macOS: bundle id or short name) to a display name, or null; <see cref="StopsWalk"/> marks
/// the shells and system processes the walk must never climb into.
/// </summary>
public sealed record HostCatalog(Func<string, string?> DisplayName, Func<string, bool> StopsWalk);

public static class KnownHosts
{
    private static readonly Dictionary<string, string> WindowsNames = new(StringComparer.Ordinal)
    {
        ["windowsterminal"] = "Windows Terminal",
        ["code"] = "VS Code",
        ["code - insiders"] = "VS Code",
        ["cursor"] = "Cursor",
        ["claude"] = "Claude",
        ["idea64"] = "JetBrains",
        ["pycharm64"] = "JetBrains",
        ["webstorm64"] = "JetBrains",
        ["rider64"] = "JetBrains",
        ["goland64"] = "JetBrains",
        ["clion64"] = "JetBrains",
        ["phpstorm64"] = "JetBrains",
        ["rubymine64"] = "JetBrains",
        ["datagrip64"] = "JetBrains",
        ["pwsh"] = "Console",
        ["powershell"] = "Console",
        ["cmd"] = "Console",
        ["mintty"] = "Git Bash",
        ["wezterm-gui"] = "WezTerm",
        ["alacritty"] = "Alacritty",
        ["hyper"] = "Hyper",
        ["tabby"] = "Tabby",
    };

    private static readonly HashSet<string> WindowsStops = new(StringComparer.Ordinal)
    {
        "explorer", "svchost", "services", "wininit", "winlogon", "userinit", "sihost", "runtimebroker",
    };

    private static readonly Dictionary<string, string> MacBundles = new(StringComparer.Ordinal)
    {
        ["com.apple.Terminal"] = "Terminal",
        ["com.googlecode.iterm2"] = "iTerm2",
        ["com.mitchellh.ghostty"] = "Ghostty",
        ["dev.warp.Warp-Stable"] = "Warp",
        ["net.kovidgoyal.kitty"] = "kitty",
        ["io.alacritty"] = "Alacritty",
        ["com.github.wez.wezterm"] = "WezTerm",
        ["com.microsoft.VSCode"] = "VS Code",
        ["com.microsoft.VSCodeInsiders"] = "VS Code",
        ["com.todesktop.230313mzl4w4u92"] = "Cursor",
        ["com.anthropic.claudefordesktop"] = "Claude",
    };

    private static readonly HashSet<string> MacStops = new(StringComparer.Ordinal) { "com.apple.finder", "com.apple.dock", "launchd" };

    public static readonly HostCatalog Windows = new(
        name => WindowsNames.GetValueOrDefault(name),
        WindowsStops.Contains);

    public static readonly HostCatalog Mac = new(
        name => name.StartsWith("com.jetbrains.", StringComparison.Ordinal) ? "JetBrains" : MacBundles.GetValueOrDefault(name),
        MacStops.Contains);
}

/// <summary>Walks from the Claude Code process up to the window-owning host. Pure: the platform supplies the table.</summary>
public static class HostChain
{
    public const int MaxDepth = 12;

    public static HostChainResult? Resolve(int startPid, IReadOnlyDictionary<int, ProcessRecord> table, HostCatalog catalog)
    {
        HostChainResult? windowed = null;
        if (!table.TryGetValue(startPid, out var node)) return null;
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (catalog.StopsWalk(node.Name)) break;
            if (node.HasWindow)
            {
                if (catalog.DisplayName(node.Name) is { } display) return new HostChainResult(node.Pid, display, true);
                windowed ??= new HostChainResult(node.Pid, node.Name, false);
            }
            if (!table.TryGetValue(node.ParentPid, out var parent)) break;
            // A parent that started after its child is a reused pid, not the real parent.
            if (parent.Pid == node.Pid || parent.StartTime > node.StartTime) break;
            node = parent;
        }
        return windowed;
    }
}
