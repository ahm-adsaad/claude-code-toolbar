import AppKit
import ClaudeToolbarCore

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var settingsStore: SettingsStore!
    private var settings = AppSettings.createDefault()
    private var controller: StatusItemController!
    private var menu: AppMenu!
    private var wakeObserver: WakeObserver?
    private var networkObserver: NetworkObserver?
    private var openSettingsObserver: NSObjectProtocol?

    func applicationDidFinishLaunching(_ notification: Notification) {
        if SingleInstance.anotherInstanceIsRunning() {
            Log.info("Another instance is running; asked it to open Settings and exiting")
            Log.flush()
            NSApp.terminate(nil)
            return
        }
        Log.info("ClaudeToolbar \(AppInfo.version) starting")

        settingsStore = SettingsStore(path: SettingsStore.defaultPath())
        settings = settingsStore.load()

        let clock = SystemClock()
        let credentials = CompositeCredentialsSource([
            KeychainCredentialsSource(clock: clock),
            FileCredentialsSource(path: CredentialsPaths.resolveFromEnvironment(), clock: clock),
        ])
        let client = OAuthUsageClient(transport: URLSessionTransport(), clock: clock)
        let monitor = UsageMonitor(credentials: credentials, client: client, clock: clock,
                                   intervalSeconds: settings.behavior.refreshIntervalSeconds)

        controller = StatusItemController(monitor: monitor, settings: settings, clock: clock,
                                          initialState: .initial(credentials: .missing(source: "Keychain")))

        menu = AppMenu()
        menu.onRefresh = { [weak self] in self?.controller.requestRefresh() }
        menu.onSettings = { [weak self] in self?.openSettings() }
        menu.onToggleLaunchAtLogin = { [weak self] in self?.toggleLaunchAtLogin() }
        menu.isLaunchAtLoginEnabled = { [weak self] in self?.settings.behavior.launchAtLogin ?? false }

        controller.onLeftClick = { [weak self] in self?.showMenu() }
        controller.onRightClick = { [weak self] in self?.showMenu() }

        wakeObserver = WakeObserver { [weak self] in
            Log.info("Woke from sleep")
            self?.controller.requestRefresh()
        }
        networkObserver = NetworkObserver { [weak self] in
            Log.info("Network reachable again")
            self?.controller.requestRefresh()
        }
        openSettingsObserver = SingleInstance.observeOpenSettings { [weak self] in
            Task { @MainActor in self?.openSettings() }
        }

        LaunchAtLogin.apply(settings.behavior.launchAtLogin)
        Log.info("Launch at login: \(LaunchAtLogin.statusDescription)")
    }

    func applicationWillTerminate(_ notification: Notification) {
        Log.info("ClaudeToolbar exiting")
        Log.flush()
    }

    private func showMenu() {
        menu.show(from: controller.item)
    }

    /// The settings window arrives in Task 14; until then a request is only logged.
    private func openSettings() {
        Log.info("Settings requested")
    }

    private func toggleLaunchAtLogin() {
        settings.behavior.launchAtLogin.toggle()
        LaunchAtLogin.apply(settings.behavior.launchAtLogin)
        saveSettings()
    }

    func saveSettings() {
        do {
            try settingsStore.save(settings)
        } catch {
            Log.error("Could not save settings: \(error.localizedDescription)")
        }
        controller.updateSettings(settings)
    }
}
