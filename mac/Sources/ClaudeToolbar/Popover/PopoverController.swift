import AppKit
import SwiftUI
import ClaudeToolbarCore

/// One popover line per live session; clicking it jumps to that session's window.
struct SessionEntry: Identifiable, Equatable {
    let id: String
    let text: String
}

@MainActor
final class PopoverController: NSObject, NSPopoverDelegate {
    @MainActor
    final class Model: ObservableObject {
        @Published var popover: PopoverModel
        @Published var colors: BarColors
        @Published var launchAtLogin: Bool
        /// One line per live Claude Code session, newest first; empty when there are none.
        @Published var sessions: [SessionEntry]
        /// Short-lived note under the sessions, e.g. "api: window closed".
        @Published var hint: String?

        init(popover: PopoverModel, colors: BarColors, launchAtLogin: Bool, sessions: [SessionEntry] = [], hint: String? = nil) {
            self.popover = popover
            self.colors = colors
            self.launchAtLogin = launchAtLogin
            self.sessions = sessions
            self.hint = hint
        }
    }

    private let popover = NSPopover()
    private let model: Model
    private var lastClosedAt: Date = .distantPast
    /// How long a close and the click that caused it are treated as one gesture: the transient
    /// popover closes on mouse-down, the status item acts on mouse-up.
    static let reopenGuard: TimeInterval = 0.3

    /// Called after the popover closes, so the host can clear what the user has now read.
    var onClose: (() -> Void)?

    init(model initial: PopoverModel, colors: BarColors, launchAtLogin: Bool,
         onRefresh: @escaping () -> Void, onSettings: @escaping () -> Void, onQuit: @escaping () -> Void,
         onLaunchAtLoginChanged: @escaping (Bool) -> Void, onJump: @escaping (String) -> Void) {
        model = Model(popover: initial, colors: colors, launchAtLogin: launchAtLogin)
        popover.behavior = .transient
        popover.animates = true
        popover.contentViewController = NSHostingController(rootView: PopoverRoot(
            model: model, onRefresh: onRefresh, onSettings: onSettings, onQuit: onQuit,
            onLaunchAtLoginChanged: onLaunchAtLoginChanged, onJump: onJump))
        super.init()
        popover.delegate = self
    }

    var isShown: Bool { popover.isShown }

    func toggle(relativeTo view: NSView) {
        if popover.isShown {
            popover.performClose(nil)
        } else {
            // A transient popover dismisses on the mouse-down that precedes the status
            // item's .leftMouseUp action, so a click that closed it would otherwise
            // immediately reopen it here; ignore a show that lands right after a close.
            guard Date().timeIntervalSince(lastClosedAt) > Self.reopenGuard else { return }
            popover.show(relativeTo: view.bounds, of: view, preferredEdge: .minY)
        }
    }

    func close() {
        if popover.isShown { popover.performClose(nil) }
    }

    func popoverDidClose(_ notification: Notification) {
        lastClosedAt = Date()
        onClose?()
    }

    func update(model newModel: PopoverModel, colors: BarColors, launchAtLogin: Bool, sessions: [SessionEntry], hint: String?) {
        if model.popover != newModel { model.popover = newModel }
        if model.colors != colors { model.colors = colors }
        if model.launchAtLogin != launchAtLogin { model.launchAtLogin = launchAtLogin }
        if model.sessions != sessions { model.sessions = sessions }
        if model.hint != hint { model.hint = hint }
    }
}

struct PopoverRoot: View {
    @ObservedObject var model: PopoverController.Model
    let onRefresh: () -> Void
    let onSettings: () -> Void
    let onQuit: () -> Void
    let onLaunchAtLoginChanged: (Bool) -> Void
    let onJump: (String) -> Void

    var body: some View {
        PopoverView(
            model: model.popover,
            colors: model.colors,
            sessions: model.sessions,
            hint: model.hint,
            onJump: onJump,
            launchAtLogin: Binding(get: { model.launchAtLogin }, set: { onLaunchAtLoginChanged($0) }),
            onRefresh: onRefresh,
            onSettings: onSettings,
            onQuit: onQuit)
    }
}
