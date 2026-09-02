using System.IO;

namespace ClaudeToolbar.App.Services;

/// <summary>Watches Claude Code's credentials file and calls back (on a thread-pool thread) 500 ms after the last change.</summary>
public sealed class CredentialsWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _debounce;

    public CredentialsWatcher(string filePath, Action onChanged)
    {
        _debounce = new Timer(_ => onChanged(), null, Timeout.Infinite, Timeout.Infinite);
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Log.Info($"Credentials directory missing, not watching: {dir}");
            return;
        }
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += Bump;
        _watcher.Created += Bump;
        _watcher.Deleted += Bump;
        _watcher.Renamed += Bump;
    }

    private void Bump(object sender, FileSystemEventArgs e) => _debounce.Change(500, Timeout.Infinite);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
