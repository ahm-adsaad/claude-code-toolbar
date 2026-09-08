using ClaudeToolbar.Core.Sessions;

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
            var key = (claudePid, ProcessTable.StartTime(claudePid));   // a reused pid gets a fresh lookup
            lock (_gate)
            {
                if (_cache.TryGetValue(key, out var cached)) return cached;
            }
            var result = HostChain.Resolve(claudePid, ProcessTable.Chain(claudePid), KnownHosts.Windows);
            var host = result is null ? null : new SessionHost(result.Pid, result.DisplayName, DateTimeOffset.UtcNow);
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
}
