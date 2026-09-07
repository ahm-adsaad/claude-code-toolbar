import AppKit
import ClaudeToolbarCore

/// Receives mouse-entered events from the status button's tracking area.
final class HoverSentinel: NSResponder {
    var onEnter: (() -> Void)?

    override func mouseEntered(with event: NSEvent) {
        onEnter?()
    }
}

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
    private let cues = ThresholdCueTracker()
    private var greeted = false
    private var waveStartedAt: Date?
    private var waveTimer: Timer?
    private var hoverSentinel: HoverSentinel?

    private(set) var state: MonitorState

    /// Session badge on Clawd's shoulder; the notification bridge drives it.
    var badge: MascotBadge = .none {
        didSet {
            guard badge != oldValue else { return }
            render()
        }
    }
    /// False while the attention badge is in its blink-off phase.
    var badgeLit = true

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
            let sentinel = HoverSentinel()
            sentinel.onEnter = { [weak self] in
                Task { @MainActor in self?.startWave(.hover) }
            }
            button.addTrackingArea(NSTrackingArea(rect: .zero, options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect], owner: sentinel, userInfo: nil))
            hoverSentinel = sentinel
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
        if MascotMode.normalize(newSettings.behavior.mascot) == MascotMode.off { cancelWave() }
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
        let model = StatusItemModelBuilder.build(state: newState, settings: settings, now: clock.now)
        if let cue = cues.observe(TrackedRows.from(model, snapshot: newState.snapshot)) { startWave(cue) }
        if newState.status == .ok && !greeted {
            greeted = true
            startWave(.greeting)
        }
        onStateChanged?(newState)
        render()
    }

    func render() {
        guard let button = statusItem.button else { return }
        let model = StatusItemModelBuilder.build(state: state, settings: settings, now: clock.now)
        let mascot = MascotModelBuilder.build(status: model, mascotMode: settings.behavior.mascot,
                                              armAngle: currentArmAngle(), badge: badge, badgeLit: badgeLit)
        button.image = StatusItemRenderer.render(model: model, mascot: mascot, settings: settings, appearance: button.effectiveAppearance)
        let text = tooltip(for: model)
        if button.toolTip != text { button.toolTip = text }
        onRender?()
    }

    /// Starts a wave for `cue` unless the mode, reduce motion, or a running wave rules it out.
    func wave(_ cue: MascotCue) {
        startWave(cue)
    }

    private func currentArmAngle() -> Double {
        guard let started = waveStartedAt else { return WaveAnimation.restAngle }
        return WaveAnimation.armAngle(progress: WaveAnimation.progress(startedAt: started, now: clock.now))
    }

    private func startWave(_ cue: MascotCue) {
        let mode = MascotMode.normalize(settings.behavior.mascot)
        if mode == MascotMode.off { return }
        if mode == MascotMode.hover && cue != .hover { return }
        if NSWorkspace.shared.accessibilityDisplayShouldReduceMotion { return }
        if waveStartedAt != nil { return }
        waveStartedAt = clock.now
        let timer = Timer(timeInterval: 1.0 / Double(WaveAnimation.framesPerSecond), repeats: true) { [weak self] _ in
            Task { @MainActor in self?.waveFrame() }
        }
        RunLoop.main.add(timer, forMode: .common)
        waveTimer = timer
        render()
    }

    private func waveFrame() {
        if let started = waveStartedAt, WaveAnimation.progress(startedAt: started, now: clock.now) >= 1 {
            waveStartedAt = nil
        }
        if waveStartedAt == nil { cancelWave() }
        render()
    }

    private func cancelWave() {
        waveStartedAt = nil
        waveTimer?.invalidate()
        waveTimer = nil
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
