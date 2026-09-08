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
    private var waveFrames = 0
    private var hoverSentinel: HoverSentinel?

    /// The wave timer runs on the wall clock while progress reads `clock`; a stubbed clock must not
    /// leave it spinning forever, so a wave also ends after a full run's worth of frames.
    private static let waveFrameBudget = Int(ceil(WaveAnimation.duration * Double(WaveAnimation.framesPerSecond)))

    private(set) var state: MonitorState

    /// Session badge on Clawd's shoulder; the notification bridge drives it.
    var badge: MascotBadge = .none {
        didSet {
            guard badge != oldValue, !isPreparingRender else { return }
            renderButton()
        }
    }
    /// False while the attention badge is in its blink-off phase.
    var badgeLit = true {
        didSet {
            guard badgeLit != oldValue, !isPreparingRender else { return }
            renderButton()
        }
    }
    /// One line about the live Claude Code sessions, shown first in the tooltip; nil when there are none.
    var sessionSummary: String? {
        didSet {
            guard sessionSummary != oldValue, !isPreparingRender else { return }
            renderButton()
        }
    }
    /// What a click on Clawd will do, shown as the tooltip's last line; nil when there is nowhere to go.
    var jumpHint: String? {
        didSet {
            guard jumpHint != oldValue, !isPreparingRender else { return }
            renderButton()
        }
    }
    /// Whether the last rendered image had Clawd in it (hidden mode, or a notice, leaves him out).
    private var mascotShown = false

    /// True while `beforeRender` runs: a badge it changes is painted by the render that follows,
    /// so the setters skip their own repaint instead of painting the same frame twice.
    private var isPreparingRender = false

    var onLeftClick: (() -> Void)?
    /// A left click that landed on Clawd; other left clicks still go to `onLeftClick`.
    var onMascotClick: (() -> Void)?
    var onRightClick: (() -> Void)?
    var onStateChanged: ((MonitorState) -> Void)?
    /// Called after every re-render (once a second) so other views can refresh countdowns.
    var onRender: (() -> Void)?
    /// Called at the top of every re-render (once a second) so the host can update the badge before it is drawn.
    var beforeRender: (() -> Void)?

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
                Task { @MainActor in _ = self?.startWave(.hover) }
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
        if let cue = cues.observe(TrackedRows.from(model, snapshot: newState.snapshot)) { _ = startWave(cue) }
        // Only spend the greeting when the wave really starts: a refused one (hover-only or off
        // mode, reduce motion) would otherwise mean the user never sees it.
        if newState.status == .ok && !greeted && startWave(.greeting) {
            greeted = true
        }
        onStateChanged?(newState)
        render()
    }

    func render() {
        isPreparingRender = true
        beforeRender?()
        isPreparingRender = false
        renderButton()
        onRender?()
    }

    /// Repaints the menu bar image only. Wave frames use this so a wave does not rebuild the popover 18 times.
    private func renderButton() {
        guard let button = statusItem.button else { return }
        let model = StatusItemModelBuilder.build(state: state, settings: settings, now: clock.now)
        let mascot = MascotModelBuilder.build(status: model, mascotMode: settings.behavior.mascot,
                                              armAngle: currentArmAngle(), badge: badge, badgeLit: badgeLit)
        mascotShown = mascot.visible && model.notice == nil
        button.image = StatusItemRenderer.render(model: model, mascot: mascot, settings: settings, appearance: button.effectiveAppearance)
        let text = tooltip(for: model)
        if button.toolTip != text { button.toolTip = text }
    }

    /// Starts a wave for `cue` unless the mode, reduce motion, or a running wave rules it out.
    func wave(_ cue: MascotCue) {
        _ = startWave(cue)
    }

    private func currentArmAngle() -> Double {
        guard let started = waveStartedAt else { return WaveAnimation.restAngle }
        return WaveAnimation.armAngle(progress: WaveAnimation.progress(startedAt: started, now: clock.now))
    }

    /// True when a wave actually started.
    @discardableResult
    private func startWave(_ cue: MascotCue) -> Bool {
        let mode = MascotMode.normalize(settings.behavior.mascot)
        if mode == MascotMode.off { return false }
        if mode == MascotMode.hover && cue != .hover { return false }
        if NSWorkspace.shared.accessibilityDisplayShouldReduceMotion { return false }
        if waveStartedAt != nil { return false }
        waveStartedAt = clock.now
        waveFrames = 0
        let timer = Timer(timeInterval: 1.0 / Double(WaveAnimation.framesPerSecond), repeats: true) { [weak self] _ in
            Task { @MainActor in self?.waveFrame() }
        }
        RunLoop.main.add(timer, forMode: .common)
        waveTimer = timer
        renderButton()
        return true
    }

    private func waveFrame() {
        waveFrames += 1
        if let started = waveStartedAt,
           WaveAnimation.progress(startedAt: started, now: clock.now) >= 1 || waveFrames >= Self.waveFrameBudget {
            waveStartedAt = nil
        }
        if waveStartedAt == nil { cancelWave() }
        renderButton()
    }

    private func cancelWave() {
        waveStartedAt = nil
        waveFrames = 0
        waveTimer?.invalidate()
        waveTimer = nil
    }

    /// The session summary leads, then either the notice or the usage rows.
    private func tooltip(for model: StatusItemModel) -> String {
        var lines: [String] = []
        if let sessionSummary { lines.append(sessionSummary) }
        if let notice = model.notice {
            lines.append(notice)
        } else {
            lines += model.rows.map { row -> String in
                var text = "\(row.label) \(row.percentText)"
                if !row.timeText.isEmpty { text += " · resets in \(row.timeText)" }
                return text
            }
            if let hint = model.hint { lines.append(hint) }
            if let last = state.lastSuccess {
                let elapsed = clock.now.timeIntervalSince(last)
                lines.append(elapsed < 60 ? "Updated just now" : "Updated \(AgoFormatter.format(ago: elapsed)) ago")
            }
        }
        if let jumpHint { lines.append(jumpHint) }
        return lines.joined(separator: "\n")
    }

    deinit {
        timer?.invalidate()
        waveTimer?.invalidate()
    }

    @objc private func buttonClicked(_ sender: Any?) {
        let event = NSApp.currentEvent
        let controlClick = event?.type == .leftMouseUp && event?.modifierFlags.contains(.control) == true
        if event?.type == .rightMouseUp || controlClick {
            onRightClick?()
        } else if isMascotHit(event) {
            onMascotClick?()
        } else {
            onLeftClick?()
        }
    }

    /// True when the click landed on Clawd: the leading part of the centred image, after the edge inset.
    private func isMascotHit(_ event: NSEvent?) -> Bool {
        guard mascotShown, let event, let button = statusItem.button, let image = button.image else { return false }
        let point = button.convert(event.locationInWindow, from: nil)
        let imageLeft = (button.bounds.width - image.size.width) / 2
        let left = imageLeft + StatusItemRenderer.edgeInset
        return point.x >= left && point.x <= left + MascotDrawing.width + StatusItemRenderer.partGap
    }
}
