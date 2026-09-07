using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.App;

public partial class App
{
    private const string TestSessionId = "test-session";
    private static readonly TimeSpan TestSessionLifetime = TimeSpan.FromSeconds(10);

    private readonly SessionTracker _sessions = new();
    private HookListener? _hooks;
    private ChimePlayer? _chime;
    private DispatcherTimer? _testSessionTimer;
    private DateTime _lastPrune = DateTime.UtcNow;
    private bool _hooksInstalled;
    private bool? _listenerEnabled;
    private int? _listenerPort;

    /// <summary>Raised on the UI thread whenever listener state, hooks state or sessions change.</summary>
    public event Action? NotificationsChanged;

    public string HookUrl => HooksConfig.HookUrl(Settings.Notifications.Port);

    public string ListenerStatus =>
        !Settings.Notifications.Enabled ? "Not listening (disabled)"
        : _hooks is null ? "Not listening"
        : _hooks.Error ?? $"Listening on {HookUrl}";

    public string? SessionSummary => _sessions.Summary;

    public string ClaudeSettingsPath => CredentialsPaths.ClaudeSettingsPathFromEnvironment();

    /// <summary>Cached: reading the settings file on every hook event would hit the disk dozens of times a minute.</summary>
    public bool HooksInstalled => _hooksInstalled;

    /// <summary>Re-reads the Claude Code settings file. Called on install/remove, on a port change and when the settings window opens.</summary>
    public void RefreshHooksInstalled()
    {
        var installed = ReadHooksInstalled();
        if (installed == _hooksInstalled) return;
        _hooksInstalled = installed;
        NotificationsChanged?.Invoke();
    }

    private bool ReadHooksInstalled()
    {
        try
        {
            return File.Exists(ClaudeSettingsPath) && HooksConfig.IsInstalled(File.ReadAllText(ClaudeSettingsPath), HookUrl);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void StartNotifications()
    {
        if (_chime is null)
        {
            try { _chime = new ChimePlayer(); }
            catch (Exception ex) { Log.Error("Chime player unavailable", ex); }
        }
        if (_hooks is null)
        {
            _hooks = new HookListener();
            _hooks.HookReceived += body => Dispatcher.InvokeAsync(() => HandleHookBody(body));
        }
        _hooksInstalled = ReadHooksInstalled();
        ApplyListenerSettings();
    }

    /// <summary>
    /// Binds or unbinds only when the user actually changed the switch or the port. Re-running it on every
    /// settings change would retry a failed bind — and log the failure again — on each tick of a slider.
    /// </summary>
    private void ApplyListenerSettings()
    {
        if (_hooks is null) return;
        var enabled = Settings.Notifications.Enabled;
        var port = Settings.Notifications.Port;
        if (enabled == _listenerEnabled && port == _listenerPort) return;

        var portChanged = port != _listenerPort;
        _listenerEnabled = enabled;
        _listenerPort = port;
        if (enabled) _hooks.Start(port); else _hooks.Stop();
        if (portChanged) _hooksInstalled = ReadHooksInstalled();
        NotificationsChanged?.Invoke();
    }

    private void StopNotifications()
    {
        _testSessionTimer?.Stop();
        _testSessionTimer = null;
        _hooks?.Dispose();
        _hooks = null;
        _chime?.Dispose();
        _chime = null;
    }

    private void HandleHookBody(string body)
    {
        try
        {
            var e = HookEventParser.Parse(body);
            if (e is null) return;
            var cue = _sessions.Apply(e, DateTimeOffset.UtcNow);
            Log.Info($"Session {e.SessionId[..Math.Min(8, e.SessionId.Length)]} ({_sessions.Sessions.FirstOrDefault(s => s.Id == e.SessionId)?.Name}): {e.Kind}{(cue is null ? "" : " → " + cue)}");
            if (cue is { } c) React(c);
            RefreshSessionUi();
        }
        catch (Exception ex)
        {
            // The dispatcher swallows exceptions raised from InvokeAsync callbacks, so log them here.
            Log.Error("Hook event failed", ex);
        }
    }

    private void React(SessionCue cue)
    {
        if (Settings.Notifications.Sound)
            _chime?.Play(cue switch { SessionCue.Attention => ChimeKind.Attention, SessionCue.Failed => ChimeKind.Failed, _ => ChimeKind.Finished });
        StartWave(cue switch { SessionCue.Attention => MascotCue.Attention, SessionCue.Failed => MascotCue.Failed, _ => MascotCue.Finished });
    }

    private void RefreshSessionUi()
    {
        _badge = _sessions.Badge;
        UpdateMascot();
        if (_widget?.IsFlyoutOpen == true) ShowFlyout();
        if (_model is not null) Tray?.SetTooltip(BuildTooltip(_model));
        NotificationsChanged?.Invoke();
    }

    /// <summary>Once a second from the widget tick: blink the attention badge and prune stale sessions.</summary>
    private void TickSessions()
    {
        var lit = _badge != MascotBadge.Attention || DateTime.UtcNow.Second % 2 == 0;
        if (lit != _badgeLit) _badgeLit = lit;
        if (DateTime.UtcNow - _lastPrune > TimeSpan.FromMinutes(1))
        {
            _lastPrune = DateTime.UtcNow;
            _sessions.Prune(DateTimeOffset.UtcNow);
            _badge = _sessions.Badge;
        }
    }

    private void AcknowledgeSessions()
    {
        if (_sessions.Badge == MascotBadge.None) return;
        _sessions.Acknowledge();
        RefreshSessionUi();
    }

    /// <summary>Returns null on success, otherwise a message for the settings window.</summary>
    public string? InstallHooks() => EditClaudeSettings(json => HooksConfig.Install(json, HookUrl));

    public string? RemoveHooks() => EditClaudeSettings(json => HooksConfig.Remove(json, HookUrl));

    private string? EditClaudeSettings(Func<string, string> edit)
    {
        var path = ClaudeSettingsPath;
        try
        {
            var current = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            if (File.Exists(path)) WriteBackup(path, path + ".claudetoolbar-bak");
            var updated = edit(current);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", updated);
            File.Move(path + ".tmp", path, overwrite: true);
            Log.Info($"Claude Code hooks updated in {path}");
            _hooksInstalled = ReadHooksInstalled();
            NotificationsChanged?.Invoke();
            return null;
        }
        catch (JsonException ex)
        {
            return $"{path} is not valid JSON: {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Copies to a sibling temp file and moves it into place, so a copy that fails part way through
    /// leaves the previous backup intact instead of a truncated one.
    /// </summary>
    private static void WriteBackup(string path, string backup)
    {
        var staged = backup + ".tmp";
        try
        {
            File.Copy(path, staged, overwrite: true);
            File.Move(staged, backup, overwrite: true);
        }
        catch
        {
            try { File.Delete(staged); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>Fires a demo attention event and clears it again shortly after, so the sample session does not linger.</summary>
    public void TestNotification()
    {
        HandleHookBody(
            $$"""{"hook_event_name":"Notification","session_id":"{{TestSessionId}}","cwd":"C:\\demo\\my-repo","notification_type":"permission_prompt","message":"Claude needs your permission (test)"}""");
        _testSessionTimer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TestSessionLifetime };
        _testSessionTimer.Stop();
        _testSessionTimer.Tick -= EndTestSession;
        _testSessionTimer.Tick += EndTestSession;
        _testSessionTimer.Start();
    }

    private void EndTestSession(object? sender, EventArgs e)
    {
        _testSessionTimer?.Stop();
        HandleHookBody($$"""{"hook_event_name":"SessionEnd","session_id":"{{TestSessionId}}","reason":"other"}""");
    }
}
