import AppKit

@MainActor
final class AppMenu: NSObject, NSMenuDelegate {
    let menu = NSMenu()
    private let launchItem: NSMenuItem
    private weak var statusItem: NSStatusItem?

    var onRefresh: (() -> Void)?
    var onSettings: (() -> Void)?
    var onToggleLaunchAtLogin: (() -> Void)?
    var isLaunchAtLoginEnabled: () -> Bool = { false }

    override init() {
        launchItem = NSMenuItem(title: "Launch at login", action: #selector(toggleLaunch(_:)), keyEquivalent: "")
        super.init()
        menu.delegate = self
        menu.autoenablesItems = false
        add("Refresh now", #selector(refresh(_:)), key: "r")
        add("Settings…", #selector(openSettings(_:)), key: ",")
        menu.addItem(.separator())
        launchItem.target = self
        menu.addItem(launchItem)
        menu.addItem(.separator())
        add("Quit Claude Toolbar", #selector(quit(_:)), key: "q")
    }

    private func add(_ title: String, _ action: Selector, key: String) {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: key)
        item.target = self
        menu.addItem(item)
    }

    /// Pops the menu under the status item with AppKit's own positioning and button highlight.
    func show(from statusItem: NSStatusItem) {
        launchItem.state = isLaunchAtLoginEnabled() ? .on : .off
        self.statusItem = statusItem
        statusItem.menu = menu
        statusItem.button?.performClick(nil)
    }

    func menuDidClose(_ menu: NSMenu) {
        statusItem?.menu = nil
        statusItem = nil
    }

    @objc private func refresh(_ sender: Any?) { onRefresh?() }
    @objc private func openSettings(_ sender: Any?) { onSettings?() }
    @objc private func toggleLaunch(_ sender: Any?) { onToggleLaunchAtLogin?() }
    @objc private func quit(_ sender: Any?) { NSApp.terminate(nil) }
}
