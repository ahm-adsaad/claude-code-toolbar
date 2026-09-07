import AppKit
import ClaudeToolbarCore

/// Owns the NSStatusItem, the 1 s tick, and the bridge from the monitor actor to the main actor.
@MainActor
final class StatusItemController {
    private let statusItem: NSStatusItem
    private let monitor: UsageMonitor
    private let clock: any ClockSource
    private var settings: AppSettings
    private var timer: Timer?
    private var appearanceObservation: NSKeyValueObservation?
    private var lastLoggedStatus: UsageStatus?

    private(set) var state: MonitorState

    var onLeftClick: (() -> Void)?
    var onRightClick: (() -> Void)?
    var onStateChanged: ((MonitorState) -> Void)?
    /// Called after every re-render (once a second) so other views can refresh countdowns.
    var onRender: (() -> Void)?

    var item: NSStatusItem { statusItem }
    var button: NSStatusBarButton? { statusItem.button }
    var currentSettings: AppSettings { settings }

    init(monitor: UsageMonitor, settings: AppSettings, clock: any ClockSource, initialState: MonitorState) {
        self.monitor = monitor
        self.settings = settings
        self.clock = clock
        self.state = initialState
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        if let button = statusItem.button {
            button.imagePosition = .imageOnly
            button.target = self
            button.action = #selector(buttonClicked(_:))
            _ = button.sendAction(on: [.leftMouseUp, .rightMouseUp])
            appearanceObservation = button.observe(\.effectiveAppearance, options: [.new]) { [weak self] _, _ in
                Task { @MainActor in self?.render() }
            }
        }
        render()

        let timer = Timer(timeInterval: 1, repeats: true) { [weak self] _ in
            Task { @MainActor in await self?.tick() }
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer

        Task { [weak self] in
            await monitor.setStateHandler { newState in
                Task { @MainActor in self?.apply(newState) }
            }
            await self?.tick()
        }
    }

    func updateSettings(_ newSettings: AppSettings) {
        let intervalChanged = newSettings.behavior.refreshIntervalSeconds != settings.behavior.refreshIntervalSeconds
        settings = newSettings
        if intervalChanged {
            let seconds = newSettings.behavior.refreshIntervalSeconds
            Task { await monitor.setIntervalSeconds(seconds) }
        }
        render()
    }

    func requestRefresh() {
        Task {
            await monitor.requestRefresh()
            await tick()
        }
    }

    private func tick() async {
        await monitor.tick()
        render()
    }

    private func apply(_ newState: MonitorState) {
        state = newState
        if newState.status != lastLoggedStatus {
            lastLoggedStatus = newState.status
            let detail = newState.message.map { " (\($0))" } ?? ""
            Log.info("Usage state: \(newState.status)\(detail)")
        }
        onStateChanged?(newState)
        render()
    }

    func render() {
        guard let button = statusItem.button else { return }
        let model = StatusItemModelBuilder.build(state: state, settings: settings, now: clock.now)
        button.image = StatusItemRenderer.render(model: model, settings: settings, appearance: button.effectiveAppearance)
        let text = tooltip(for: model)
        if button.toolTip != text { button.toolTip = text }
        onRender?()
    }

    private func tooltip(for model: StatusItemModel) -> String {
        if let notice = model.notice { return notice }
        var lines = model.rows.map { row -> String in
            var text = "\(row.label) \(row.percentText)"
            if !row.timeText.isEmpty { text += " · resets in \(row.timeText)" }
            return text
        }
        if let hint = model.hint { lines.append(hint) }
        if let last = state.lastSuccess {
            let elapsed = clock.now.timeIntervalSince(last)
            lines.append(elapsed < 60 ? "Updated just now" : "Updated \(AgoFormatter.format(ago: elapsed)) ago")
        }
        return lines.joined(separator: "\n")
    }

    @objc private func buttonClicked(_ sender: Any?) {
        let event = NSApp.currentEvent
        let controlClick = event?.type == .leftMouseUp && event?.modifierFlags.contains(.control) == true
        if event?.type == .rightMouseUp || controlClick {
            onRightClick?()
        } else {
            onLeftClick?()
        }
    }
}
