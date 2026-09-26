import AppKit

/// Reports whether anyone can see the menu bar: not while the displays sleep, the screen is locked,
/// or another user's session is in front. Calls back only when that answer changes.
@MainActor
final class VisibilityObserver {
    private var tokens: [(center: NotificationCenter, token: NSObjectProtocol)] = []
    private var screensAsleep = false
    private var locked = false
    private var sessionInactive = false
    private let onChange: @MainActor @Sendable (_ visible: Bool) -> Void

    var visible: Bool { !(screensAsleep || locked || sessionInactive) }

    init(onChange: @escaping @MainActor @Sendable (_ visible: Bool) -> Void) {
        self.onChange = onChange
        let workspace = NSWorkspace.shared.notificationCenter
        observe(workspace, NSWorkspace.screensDidSleepNotification) { $0.screensAsleep = true }
        observe(workspace, NSWorkspace.screensDidWakeNotification) { $0.screensAsleep = false }
        observe(workspace, NSWorkspace.sessionDidResignActiveNotification) { $0.sessionInactive = true }
        observe(workspace, NSWorkspace.sessionDidBecomeActiveNotification) { $0.sessionInactive = false }
        let distributed = DistributedNotificationCenter.default()
        observe(distributed, Notification.Name("com.apple.screenIsLocked")) { $0.locked = true }
        observe(distributed, Notification.Name("com.apple.screenIsUnlocked")) { $0.locked = false }
    }

    private func observe(_ center: NotificationCenter, _ name: Notification.Name,
                         _ update: @escaping @MainActor @Sendable (VisibilityObserver) -> Void) {
        let token = center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in
                guard let self else { return }
                let before = self.visible
                update(self)
                if self.visible != before { self.onChange(self.visible) }
            }
        }
        tokens.append((center, token))
    }

    deinit {
        for entry in tokens {
            entry.center.removeObserver(entry.token)
        }
    }
}
