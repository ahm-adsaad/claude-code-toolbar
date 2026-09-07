import AppKit
import ClaudeToolbarCore

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var settingsStore: SettingsStore!
    private var settings = AppSettings.createDefault()
    private var controller: StatusItemController!
    private var menu: AppMenu!
    private var popover: PopoverController!
    private var settingsWindow: SettingsWindowController?
    private var settingsModel: SettingsModel?
    private var wakeObserver: WakeObserver?
    private var networkObserver: NetworkObserver?
    private var openSettingsObserver: NSObjectProtocol?

    private let sessions = SessionTracker()
    private var hookListener: HookListener?
    private var chime: ChimePlayer?
    private var lastPrune = Date()
    /// What the listener was last asked for, so a failed bind is not retried on every settings change.
    private var listenerEnabled: Bool?
    private var listenerPort: Int?
    private var testSessionWork: DispatchWorkItem?

    private static let testSessionId = "test-session"
    private static let testSessionLifetime: TimeInterval = 10

    private static let timeFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .none
        f.timeStyle = .short
        return f
    }()

    private static let dayTimeFormatter: DateFormatter = {
        let f = DateFormatter()
        f.setLocalizedDateFormatFromTemplate("EEE j:mm")
        return f
    }()

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
        menu.onToggleLaunchAtLogin = { [weak self] in self?.setLaunchAtLogin(!(self?.settings.behavior.launchAtLogin ?? false)) }
        menu.isLaunchAtLoginEnabled = { [weak self] in self?.settings.behavior.launchAtLogin ?? false }

        popover = PopoverController(
            model: popoverModel(),
            colors: BarColors(settings: settings),
            launchAtLogin: settings.behavior.launchAtLogin,
            onRefresh: { [weak self] in self?.controller.requestRefresh() },
            onSettings: { [weak self] in
                self?.popover.close()
                self?.openSettings()
            },
            onQuit: { NSApp.terminate(nil) },
            onLaunchAtLoginChanged: { [weak self] enabled in self?.setLaunchAtLogin(enabled) })

        controller.onLeftClick = { [weak self] in self?.togglePopover() }
        controller.onRightClick = { [weak self] in self?.showMenu() }
        controller.onRender = { [weak self] in
            guard let self, self.popover.isShown else { return }
            self.updatePopover()
        }
        controller.onStateChanged = { [weak self] state in
            self?.settingsModel?.account = state
        }
        controller.beforeRender = { [weak self] in self?.tickSessions() }

        chime = ChimePlayer()
        let listener = HookListener()
        listener.onHook = { [weak self] body in self?.handleHook(body) }
        listener.onStateChanged = { [weak self] in
            guard let self else { return }
            self.settingsModel?.listenerStatus = self.listenerStatus
        }
        hookListener = listener
        applyListenerSettings()

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

    /// Finder, Dock and Spotlight re-activate a running app instead of starting a second process.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        openSettings()
        return true
    }

    func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
        true
    }

    func applicationWillTerminate(_ notification: Notification) {
        settingsModel?.flushPendingSave()
        testSessionWork?.cancel()
        hookListener?.stop()
        Log.info("ClaudeToolbar exiting")
        Log.flush()
    }

    private func showMenu() {
        popover.close()
        menu.show(from: controller.item)
    }

    private func togglePopover() {
        guard let button = controller.button else { return }
        if !popover.isShown { acknowledgeSessions() }
        updatePopover()
        popover.toggle(relativeTo: button)
    }

    private func updatePopover() {
        popover.update(model: popoverModel(), colors: BarColors(settings: settings),
                       launchAtLogin: settings.behavior.launchAtLogin, sessionSummary: sessions.summary)
    }

    private func popoverModel() -> PopoverModel {
        PopoverModelBuilder.build(state: controller.state, settings: settings, now: Date(), formatClock: Self.formatClock)
    }

    static func formatClock(_ date: Date) -> String {
        Calendar.current.isDateInToday(date) ? timeFormatter.string(from: date) : dayTimeFormatter.string(from: date)
    }

    private func openSettings() {
        if settingsWindow == nil {
            let model = SettingsModel(
                settings: settings,
                account: controller.state,
                launchAtLoginStatus: LaunchAtLogin.statusDescription,
                onApply: { [weak self] updated in self?.applySettings(updated) },
                onSave: { [weak self] updated in self?.persistSettings(updated) },
                onRefresh: { [weak self] in self?.controller.requestRefresh() },
                listenerStatus: listenerStatus,
                hooksInstalled: HooksInstaller.isInstalled(url: hookUrl),
                sessionSummary: sessions.summary,
                onInstallHooks: { [weak self] in
                    guard let self else { return nil }
                    return HooksInstaller.install(url: self.hookUrl)
                },
                onRemoveHooks: { [weak self] in
                    guard let self else { return nil }
                    return HooksInstaller.remove(url: self.hookUrl)
                },
                onTestNotification: { [weak self] in self?.testNotification() },
                readHooksInstalled: { [weak self] in
                    guard let self else { return false }
                    return HooksInstaller.isInstalled(url: self.hookUrl)
                })
            settingsModel = model
            settingsWindow = SettingsWindowController(model: model)
        }
        settingsModel?.account = controller.state
        settingsModel?.launchAtLoginStatus = LaunchAtLogin.statusDescription
        settingsModel?.listenerStatus = listenerStatus
        settingsModel?.hooksInstalled = HooksInstaller.isInstalled(url: hookUrl)
        settingsModel?.sessionSummary = sessions.summary
        settingsWindow?.show()
    }

    private func applySettings(_ updated: AppSettings) {
        let launchChanged = updated.behavior.launchAtLogin != settings.behavior.launchAtLogin
        let notificationsChanged = updated.notifications != settings.notifications
        settings = updated
        if launchChanged {
            LaunchAtLogin.apply(updated.behavior.launchAtLogin)
            settingsModel?.launchAtLoginStatus = LaunchAtLogin.statusDescription
        }
        if notificationsChanged { applyListenerSettings() }
        controller.updateSettings(settings)
        updatePopover()
    }

    // MARK: - Claude Code sessions

    var hookUrl: String { HooksConfig.hookUrl(port: settings.notifications.port) }

    var listenerStatus: String {
        if !settings.notifications.enabled { return "Not listening (disabled)" }
        guard let listener = hookListener else { return "Not listening" }
        return listener.error ?? "Listening on \(hookUrl)"
    }

    /// Binds or unbinds only when the switch or the port actually changed: re-running it on every
    /// settings change would retry - and log - a failed bind on each tick of a slider.
    private func applyListenerSettings() {
        guard let listener = hookListener else { return }
        let enabled = settings.notifications.enabled
        let port = settings.notifications.port
        defer { settingsModel?.listenerStatus = listenerStatus }
        guard enabled != listenerEnabled || port != listenerPort else { return }
        listenerEnabled = enabled
        listenerPort = port
        if enabled {
            listener.start(port: UInt16(clamping: port))
        } else {
            listener.stop()
        }
    }

    private func handleHook(_ body: String) {
        guard let event = HookEventParser.parse(body) else { return }
        let cue = sessions.apply(event, now: Date())
        Log.info("Session \(event.sessionId.prefix(8)): \(event.kind)\(cue.map { " \u{2192} \($0)" } ?? "")")
        if let cue {
            if settings.notifications.sound { chime?.play(Self.chimeKind(cue)) }
            controller.wave(Self.mascotCue(cue))
        }
        refreshSessionUi()
    }

    private static func chimeKind(_ cue: SessionCue) -> ChimeKind {
        switch cue {
        case .attention: return .attention
        case .failed: return .failed
        case .finished: return .finished
        }
    }

    private static func mascotCue(_ cue: SessionCue) -> MascotCue {
        switch cue {
        case .attention: return .attention
        case .failed: return .failed
        case .finished: return .finished
        }
    }

    private func refreshSessionUi() {
        controller.badge = sessions.badge
        if popover.isShown { updatePopover() }
        settingsModel?.sessionSummary = sessions.summary
    }

    /// Once a second, from the status item's own tick: blink the attention badge and prune stale sessions.
    private func tickSessions() {
        controller.badgeLit = controller.badge != .attention || Int(Date().timeIntervalSince1970) % 2 == 0
        if Date().timeIntervalSince(lastPrune) > 60 {
            lastPrune = Date()
            sessions.prune(now: Date())
            controller.badge = sessions.badge
        }
    }

    private func acknowledgeSessions() {
        guard sessions.badge != .none else { return }
        sessions.acknowledge()
        refreshSessionUi()
    }

    /// Fires a demo attention event and ends the sample session shortly after so it does not linger.
    func testNotification() {
        handleHook(##"{"hook_event_name":"Notification","session_id":"\##(Self.testSessionId)","cwd":"/demo/my-repo","notification_type":"permission_prompt","message":"Claude needs your permission (test)"}"##)
        testSessionWork?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self else { return }
            self.handleHook(##"{"hook_event_name":"SessionEnd","session_id":"\##(Self.testSessionId)","reason":"other"}"##)
        }
        testSessionWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.testSessionLifetime, execute: work)
    }

    private func persistSettings(_ updated: AppSettings) {
        do {
            try settingsStore.save(updated)
        } catch {
            Log.error("Could not save settings: \(error.localizedDescription)")
        }
    }

    private func setLaunchAtLogin(_ enabled: Bool) {
        guard settings.behavior.launchAtLogin != enabled else { return }
        if let model = settingsModel {
            model.settings.behavior.launchAtLogin = enabled   // flows back through onApply/onSave
        } else {
            settings.behavior.launchAtLogin = enabled
            LaunchAtLogin.apply(enabled)
            saveSettings()
        }
    }

    func saveSettings() {
        persistSettings(settings)
        controller.updateSettings(settings)
        updatePopover()
    }
}
