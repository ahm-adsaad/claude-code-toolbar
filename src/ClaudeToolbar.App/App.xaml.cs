using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Tray;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private TrayIcon? _tray;
    private AppMenu? _menu;
    private DispatcherTimer? _saveTimer;

    public AppSettings Settings { get; private set; } = AppSettings.CreateDefault();
    public SettingsStore SettingsStore { get; private set; } = new(SettingsStore.DefaultPath());

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--dump-taskbar", StringComparer.OrdinalIgnoreCase))
        {
            DumpTaskbar();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) => { Log.Error("Unhandled UI exception", args.Exception); args.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error("Unobserved task exception", args.Exception); args.SetObserved(); };

        _instance = SingleInstance.Acquire(() => Dispatcher.InvokeAsync(OpenSettings));
        if (!_instance.IsFirst)
        {
            Log.Info("Another instance is running; asked it to open settings and exiting");
            Shutdown();
            return;
        }

        Settings = SettingsStore.Load();
        if (!File.Exists(SettingsStore.Path)) SettingsStore.Save(Settings);
        TrySafe(() => StartupRegistration.Apply(Settings.Behavior.RunAtStartup), "startup registration");

        _menu = new AppMenu(Settings.Behavior.RunAtStartup);
        _menu.ExitRequested += Shutdown;
        _menu.SettingsRequested += OpenSettings;
        _menu.RefreshRequested += RefreshNow;
        _menu.RunAtStartupToggled += on =>
        {
            Settings.Behavior.RunAtStartup = on;
            TrySafe(() => StartupRegistration.Apply(on), "startup registration");
            SaveSettingsDebounced();
        };

        _tray = new TrayIcon("Claude Toolbar");
        _tray.MenuRequested += _menu.Show;
        _tray.SettingsRequested += OpenSettings;

        Log.Info("Started");
        OnStartupCore(e);
    }

    /// <summary>Extended by later tasks (widget + monitor wiring).</summary>
    partial void OnStartupCore(StartupEventArgs e);

    public void OpenSettings() => OpenSettingsCore();
    partial void OpenSettingsCore();

    public void RefreshNow() => RefreshNowCore();
    partial void RefreshNowCore();

    public TrayIcon? Tray => _tray;

    public void SaveSettingsDebounced()
    {
        _saveTimer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(300) };
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTick;
        _saveTimer.Tick += SaveTick;
        _saveTimer.Start();
    }

    private void SaveTick(object? sender, EventArgs e)
    {
        _saveTimer!.Stop();
        TrySafe(() => SettingsStore.Save(Settings), "save settings");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _saveTimer?.Stop();
        OnExitCore();
        _tray?.Dispose();
        _menu?.Dispose();
        _instance?.Dispose();
        Log.Info("Exited");
        base.OnExit(e);
    }

    partial void OnExitCore();

    public static void TrySafe(Action action, string what)
    {
        try { action(); }
        catch (Exception ex) { Log.Error($"Failed: {what}", ex); }
    }

    private static void DumpTaskbar()
    {
        var layout = Interop.TaskbarLocator.Locate();
        var text = layout is null
            ? "Taskbar not found"
            : $"Tray HWND: 0x{layout.TrayHwnd:X}\nNotify HWND: 0x{layout.NotifyHwnd:X}\nTaskbar (docked): {layout.Taskbar}\nTaskbar (now): {layout.TaskbarNow}\nNotify: {layout.Notify}\nMonitor: {layout.Monitor}\nAutoHide: {layout.AutoHide}\nExplorer PID: {layout.ExplorerPid}";
        var path = Path.Combine(Log.LogDirectory, "taskbar-dump.txt");
        Directory.CreateDirectory(Log.LogDirectory);
        File.WriteAllText(path, text);
        Log.Info($"Wrote taskbar dump to {path}");
    }
}
