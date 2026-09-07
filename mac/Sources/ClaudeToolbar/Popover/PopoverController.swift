import AppKit
import SwiftUI
import ClaudeToolbarCore

@MainActor
final class PopoverController {
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

    init(model initial: PopoverModel, colors: BarColors, launchAtLogin: Bool,
         onRefresh: @escaping () -> Void, onSettings: @escaping () -> Void, onQuit: @escaping () -> Void,
         onLaunchAtLoginChanged: @escaping (Bool) -> Void) {
        model = Model(popover: initial, colors: colors, launchAtLogin: launchAtLogin)
        popover.behavior = .transient
        popover.animates = true
        popover.contentViewController = NSHostingController(rootView: PopoverRoot(
            model: model, onRefresh: onRefresh, onSettings: onSettings, onQuit: onQuit,
            onLaunchAtLoginChanged: onLaunchAtLoginChanged))
    }

    var isShown: Bool { popover.isShown }

    func toggle(relativeTo view: NSView) {
        if popover.isShown {
            popover.performClose(nil)
        } else {
            popover.show(relativeTo: view.bounds, of: view, preferredEdge: .minY)
        }
    }

    func close() {
        if popover.isShown { popover.performClose(nil) }
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
