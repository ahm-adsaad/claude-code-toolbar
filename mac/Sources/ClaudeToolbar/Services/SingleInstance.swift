import AppKit

enum SingleInstance {
    static let openSettingsNotification = Notification.Name("io.github.ahm-adsaad.ClaudeToolbar.openSettings")

    /// True when another copy of this app is already running. In that case the other copy has been asked to open Settings.
    static func anotherInstanceIsRunning() -> Bool {
        guard let bundleIdentifier = Bundle.main.bundleIdentifier else { return false }
        let ownPid = ProcessInfo.processInfo.processIdentifier
        let others = NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier)
            .filter { $0.processIdentifier != ownPid }
        guard !others.isEmpty else { return false }
        DistributedNotificationCenter.default().postNotificationName(openSettingsNotification, object: nil, userInfo: nil, deliverImmediately: true)
        return true
    }

    static func observeOpenSettings(_ handler: @escaping () -> Void) -> NSObjectProtocol {
        DistributedNotificationCenter.default().addObserver(forName: openSettingsNotification, object: nil, queue: .main) { _ in
            handler()
        }
    }
}
