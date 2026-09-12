using ClaudeToolbar.Core.Sessions;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Services;

/// <summary>
/// Maps a hook request's client port to the window-owning host of that Claude Code process.
/// Called on the listener's thread-pool thread while the connection is still open; a session costs one lookup.
/// </summary>
public sealed class HostResolver
{
    private readonly object _gate = new();
    private readonly Dictionary<(int Pid, DateTimeOffset Start), SessionHost?> _cache = new();

    public SessionHost? Resolve(int clientPort, int listenerPort)
    {
        try
        {
            if (PeerProcess.OwningPid(clientPort, listenerPort) is not { } claudePid) return null;
            var start = ProcessTable.StartTime(claudePid);
            var key = (claudePid, start);   // a reused pid gets a fresh lookup
            lock (_gate)
            {
                if (_cache.TryGetValue(key, out var cached)) return cached;
            }
            var host = FromConsole(claudePid, start) ?? FromChain(claudePid, start);
            lock (_gate)
            {
                if (_cache.Count > 256) _cache.Clear();
                _cache[key] = host;
            }
            Log.Info(host is null
                ? $"Session host: pid {claudePid} has no window-owning ancestor"
                : $"Session host: pid {claudePid} → {host.Name} (pid {host.Pid})");
            return host;
        }
        catch (Exception ex)
        {
            Log.Error("Session host lookup failed", ex);
            return null;
        }
    }

    /// <summary>
    /// A Claude Code process in a terminal is attached to a console whose window belongs to the terminal that
    /// shows it. That holds even when the process chain never reaches the terminal — a shell that Windows handed
    /// to the default terminal app has no terminal among its ancestors — and it names the exact window when the
    /// terminal has several. Null for an editor's embedded Claude Code, whose console has no visible window.
    /// </summary>
    private static SessionHost? FromConsole(int claudePid, DateTimeOffset start)
    {
        if (ConsoleProbe.Read(claudePid) is not { } console || !ProcessTable.IsCandidateWindow(console.Window)) return null;
        GetWindowThreadProcessId(console.Window, out var ownerPid);
        if (ownerPid == 0 || ProcessTable.Name((int)ownerPid) is not { } name) return null;
        return new SessionHost((int)ownerPid, KnownHosts.Windows.DisplayName(name) ?? name, DateTimeOffset.UtcNow, claudePid, start);
    }

    private static SessionHost? FromChain(int claudePid, DateTimeOffset start)
    {
        var result = HostChain.Resolve(claudePid, ProcessTable.Chain(claudePid), KnownHosts.Windows);
        return result is null ? null : new SessionHost(result.Pid, result.DisplayName, DateTimeOffset.UtcNow, claudePid, start);
    }
}
