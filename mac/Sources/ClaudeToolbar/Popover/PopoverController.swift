import AppKit
import SwiftUI
import ClaudeToolbarCore

@MainActor
final class PopoverController: NSObject, NSPopoverDelegate {
    @MainActor
    final class Model: ObservableObject {
        @Published var popover: PopoverModel
        @Published var colors: BarColors
        @Published var launchAtLogin: Bool

        init(popover: PopoverModel, colors: BarColors, launchAtLogin: Bool) {
            self.popover = popover
            self.colors = colors
            self.launchAtLogin = launchAtLogin
        }
    }

    private let popover = NSPopover()
    private let model: Model
    private var lastClosedAt: Date = .distantPast
    private static let reopenGuard: TimeInterval = 0.3

    init(model initial: PopoverModel, colors: BarColors, launchAtLogin: Bool,
         onRefresh: @escaping () -> Void, onSettings: @escaping () -> Void, onQuit: @escaping () -> Void,
         onLaunchAtLoginChanged: @escaping (Bool) -> Void) {
        model = Model(popover: initial, colors: colors, launchAtLogin: launchAtLogin)
        popover.behavior = .transient
        popover.animates = true
        popover.contentViewController = NSHostingController(rootView: PopoverRoot(
            model: model, onRefresh: onRefresh, onSettings: onSettings, onQuit: onQuit,
            onLaunchAtLoginChanged: onLaunchAtLoginChanged))
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
    }

    func update(model newModel: PopoverModel, colors: BarColors, launchAtLogin: Bool) {
        if model.popover != newModel { model.popover = newModel }
        if model.colors != colors { model.colors = colors }
        if model.launchAtLogin != launchAtLogin { model.launchAtLogin = launchAtLogin }
    }
}

struct PopoverRoot: View {
    @ObservedObject var model: PopoverController.Model
    let onRefresh: () -> Void
    let onSettings: () -> Void
    let onQuit: () -> Void
    let onLaunchAtLoginChanged: (Bool) -> Void

    var body: some View {
        PopoverView(
            model: model.popover,
            colors: model.colors,
            launchAtLogin: Binding(get: { model.launchAtLogin }, set: { onLaunchAtLoginChanged($0) }),
            onRefresh: onRefresh,
            onSettings: onSettings,
            onQuit: onQuit)
    }
}
