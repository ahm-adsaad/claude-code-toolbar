using System.IO;
using System.Text.Json;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.App;

public partial class App
{
    private readonly SessionTracker _sessions = new();
    private HookListener? _hooks;
    private ChimePlayer? _chime;
    private DateTime _lastPrune = DateTime.UtcNow;

    /// <summary>Raised on the UI thread whenever listener state, hooks state or sessions change.</summary>
    public event Action? NotificationsChanged;

    public string HookUrl => HooksConfig.HookUrl(Settings.Notifications.Port);

    public string ListenerStatus =>
        !Settings.Notifications.Enabled ? "Not listening (disabled)"
        : _hooks is null ? "Not listening"
        : _hooks.Error ?? $"Listening on {HookUrl}";

    public string? SessionSummary => _sessions.Summary;

    public string ClaudeSettingsPath => CredentialsPaths.ClaudeSettingsPathFromEnvironment();

    public bool HooksInstalled
    {
        get
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
        ApplyListenerSettings();
    }

    private void ApplyListenerSettings()
    {
        if (_hooks is null) return;
        if (!Settings.Notifications.Enabled)
        {
            _hooks.Stop();
        }
        else if (_hooks.Port != Settings.Notifications.Port || !_hooks.IsListening)
        {
            _hooks.Start(Settings.Notifications.Port);
        }
        NotificationsChanged?.Invoke();
    }

    private void StopNotifications()
    {
        _hooks?.Dispose();
        _hooks = null;
        _chime?.Dispose();
        _chime = null;
    }

    private void HandleHookBody(string body)
    {
        var e = HookEventParser.Parse(body);
        if (e is null) return;
        var cue = _sessions.Apply(e, DateTimeOffset.UtcNow);
        Log.Info($"Session {e.SessionId[..Math.Min(8, e.SessionId.Length)]} ({_sessions.Sessions.FirstOrDefault(s => s.Id == e.SessionId)?.Name}): {e.Kind}{(cue is null ? "" : " → " + cue)}");
        if (cue is { } c) React(c);
        RefreshSessionUi();
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
            if (File.Exists(path)) File.Copy(path, path + ".claudetoolbar-bak", overwrite: true);
            var updated = edit(current);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", updated);
            File.Move(path + ".tmp", path, overwrite: true);
            Log.Info($"Claude Code hooks updated in {path}");
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

    public void TestNotification() => HandleHookBody(
        """{"hook_event_name":"Notification","session_id":"test-session","cwd":"C:\\demo\\my-repo","notification_type":"permission_prompt","message":"Claude needs your permission (test)"}""");
}
