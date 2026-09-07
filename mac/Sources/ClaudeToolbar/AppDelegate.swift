import AppKit
import ClaudeToolbarCore

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private var openSettingsObserver: NSObjectProtocol?

    func applicationDidFinishLaunching(_ notification: Notification) {
        if SingleInstance.anotherInstanceIsRunning() {
            Log.info("Another instance is running; asked it to open Settings and exiting")
            NSApp.terminate(nil)
            return
        }
        Log.info("ClaudeToolbar \(AppInfo.version) starting")

        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.title = "Claude"
        let menu = NSMenu()
        let quit = NSMenuItem(title: "Quit Claude Toolbar", action: #selector(quit(_:)), keyEquivalent: "q")
        quit.target = self
        menu.addItem(quit)
        item.menu = menu
        statusItem = item

        openSettingsObserver = SingleInstance.observeOpenSettings {
            Log.info("Open Settings requested by a second launch")
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        Log.info("ClaudeToolbar exiting")
    }

    @objc private func quit(_ sender: Any?) {
        NSApp.terminate(nil)
    }
}
