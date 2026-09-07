using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using ClaudeToolbar.App.Interop;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Widget;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Time;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;
using Microsoft.Win32;

namespace ClaudeToolbar.App;

public partial class App
{
    private static readonly TimeSpan NoCredentialsRetry = TimeSpan.FromSeconds(30);

    private WidgetWindow? _widget;
    private WidgetController? _controller;
    private UsageMonitor? _monitor;
    private CredentialsWatcher? _credentialsWatcher;
    private HttpClient? _http;
    private DispatcherTimer? _tick;
    private WidgetTheme? _theme;
    private WidgetModel? _model;
    private DateTime _lastNoCredentialsRetry = DateTime.MinValue;
    private bool? _startupApplied;
    private readonly ThresholdCueTracker _cues = new();
    private DispatcherTimer? _waveTimer;
    private DateTimeOffset? _waveStartedAt;
    private bool _greeted;
    private MascotBadge _badge = MascotBadge.None;
    private bool _badgeLit = true;

    public event Action<MonitorState>? MonitorStateChanged;

    public MonitorState? CurrentState => _monitor?.State;

    partial void OnStartupCore(StartupEventArgs e)
    {
        var clock = SystemClock.Instance;
        var credentialsPath = CredentialsPaths.ResolveFromEnvironment();

        _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) });
        _monitor = new UsageMonitor(
            new FileCredentialsSource(credentialsPath, clock),
            new UsageClient(_http, clock),
            clock,
            new RefreshScheduler(clock, Settings.Behavior.RefreshIntervalSeconds));
        _monitor.StateChanged += state => Dispatcher.InvokeAsync(() => OnMonitorState(state));
        _credentialsWatcher = new CredentialsWatcher(credentialsPath, () => Dispatcher.InvokeAsync(() =>
        {
            Log.Info("Credentials file changed");
            _monitor.OnCredentialsChanged();
        }));

        _widget = new WidgetWindow();
        _widget.Clicked += OpenSettings;
        _widget.MenuRequested += () => _menu?.Show();
        _widget.FlyoutRequested += ShowFlyout;
        _controller = new WidgetController(_widget, new TaskbarTracker(_widget), () => Settings);
        _theme = WidgetTheme.FromSettings(Settings.Appearance);
        RenderWidget(_monitor.State);
        _controller.Start();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        _tick = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => Tick();
        _tick.Start();
        _widget.HoverStarted += () => StartWave(MascotCue.Hover);
        _waveTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / WaveAnimation.FramesPerSecond) };
        _waveTimer.Tick += (_, _) => WaveFrame();
        Log.Info($"Monitoring credentials at {credentialsPath}");
    }

    partial void RefreshNowCore()
    {
        _monitor?.RequestRefresh();
        _controller?.Relocate();
    }

    /// <summary>Re-applies the current <see cref="Settings"/> to the running widget. Called by the settings window on every change.</summary>
    public void ApplySettingsLive()
    {
        _theme = WidgetTheme.FromSettings(Settings.Appearance);
        if (_monitor is not null && _monitor.Scheduler.IntervalSeconds != Settings.Behavior.RefreshIntervalSeconds)
        {
            _monitor.Scheduler.IntervalSeconds = Settings.Behavior.RefreshIntervalSeconds;
            _monitor.RequestRefresh();
        }
        if (_startupApplied != Settings.Behavior.RunAtStartup)
        {
            _startupApplied = Settings.Behavior.RunAtStartup;
            TrySafe(() => StartupRegistration.Apply(Settings.Behavior.RunAtStartup), "startup registration");
            _menu?.SetRunAtStartup(Settings.Behavior.RunAtStartup);
        }
        if (_monitor is not null) RenderWidget(_monitor.State);
        _controller?.Reposition();
        if (MascotMode.Normalize(Settings.Behavior.Mascot) == MascotMode.Off)
        {
            _waveStartedAt = null;
            _waveTimer?.Stop();
        }
        UpdateMascot();
    }

    private void OnMonitorState(MonitorState state)
    {
        Log.Info($"Usage state: {state.Status}{(state.Message is null ? string.Empty : " — " + state.Message)}");
        RenderWidget(state);
        ObserveCues(state);
        if (state.Status == UsageStatus.Ok && !_greeted)
        {
            _greeted = true;
            StartWave(MascotCue.Greeting);
        }
        MonitorStateChanged?.Invoke(state);
    }

    private void RenderWidget(MonitorState state)
    {
        if (_widget is null) return;
        _model = WidgetModelBuilder.Build(state, Settings, DateTimeOffset.UtcNow);
        _widget.Render(_model, Settings.Rows, _theme ??= WidgetTheme.FromSettings(Settings.Appearance));
        UpdateMascot();
        _controller?.Reposition();
        Tray?.SetTooltip(BuildTooltip(_model));
    }

    private static string BuildTooltip(WidgetModel model)
    {
        if (model.Rows.Count == 0) return "Claude Toolbar · " + (model.Notice ?? string.Empty);
        return "Claude Toolbar · " + string.Join(" · ", model.Rows.Select(r => $"{r.Label} {r.PercentText}"));
    }

    private void ShowFlyout()
    {
        if (_widget is null || _monitor is null || _theme is null) return;
        var flyout = FlyoutModelBuilder.Build(_monitor.State, DateTimeOffset.UtcNow, t => t.ToLocalTime().ToString("HH:mm"));
        _widget.ShowFlyout(flyout, _theme);
    }

    private void Tick()
    {
        if (_monitor is null || _widget is null) return;

        if (_monitor.State.Status == UsageStatus.NoCredentials && DateTime.UtcNow - _lastNoCredentialsRetry > NoCredentialsRetry)
        {
            _lastNoCredentialsRetry = DateTime.UtcNow;
            _monitor.RequestRefresh();
        }

        _ = SafeTickAsync();

        _model = WidgetModelBuilder.Build(_monitor.State, Settings, DateTimeOffset.UtcNow);
        _widget.UpdateTimes(_model);
        UpdateMascot();
        if (_widget.IsFlyoutOpen) ShowFlyout();
    }

    private void StartWave(MascotCue cue)
    {
        var mode = MascotMode.Normalize(Settings.Behavior.Mascot);
        if (mode == MascotMode.Off) return;
        if (mode == MascotMode.Hover && cue != MascotCue.Hover) return;
        if (!SystemParameters.ClientAreaAnimation) return;
        if (_waveStartedAt is not null) return;
        _waveStartedAt = DateTimeOffset.UtcNow;
        _waveTimer?.Start();
        UpdateMascot();
    }

    private void WaveFrame()
    {
        if (_waveStartedAt is { } started && WaveAnimation.Progress(started, DateTimeOffset.UtcNow) >= 1)
            _waveStartedAt = null;
        if (_waveStartedAt is null) _waveTimer?.Stop();
        UpdateMascot();
    }

    private double CurrentArmAngle() => _waveStartedAt is { } started
        ? WaveAnimation.ArmAngle(WaveAnimation.Progress(started, DateTimeOffset.UtcNow))
        : WaveAnimation.RestAngle;

    private void UpdateMascot()
    {
        if (_widget is null || _model is null) return;
        _widget.UpdateMascot(MascotModelBuilder.Build(_model, Settings.Behavior.Mascot, CurrentArmAngle(), _badge, _badgeLit));
    }

    private void ObserveCues(MonitorState state)
    {
        if (_model is null) return;
        if (_cues.Observe(TrackedRows.From(_model, state.Snapshot)) is { } cue) StartWave(cue);
    }

    private async Task SafeTickAsync()
    {
        try
        {
            await _monitor!.TickAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Error("Usage tick failed", ex);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        Dispatcher.InvokeAsync(() =>
        {
            Log.Info("Resumed from sleep");
            _monitor?.RequestRefresh();
            _controller?.Relocate();
        });
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(() => _controller?.Relocate());

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable) return;
        Dispatcher.InvokeAsync(() =>
        {
            Log.Info("Network available");
            _monitor?.RequestRefresh();
        });
    }

    partial void OnExitCore()
    {
        _tick?.Stop();
        _waveTimer?.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _credentialsWatcher?.Dispose();
        _controller?.Dispose();
        _widget?.Close();
        _http?.Dispose();
    }
}
