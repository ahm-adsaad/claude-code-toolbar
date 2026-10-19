import AppKit

@MainActor
final class WakeObserver {
    private var tokens: [NSObjectProtocol] = []

    /// `onSleep` gets the time the sleep was announced: the task that delivers it may only run after the Mac wakes.
    init(onSleep: @escaping @MainActor (Date) -> Void, onWake: @escaping @MainActor () -> Void) {
        let center = NSWorkspace.shared.notificationCenter
        tokens.append(center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { _ in
            let at = Date()
            Task { @MainActor in onSleep(at) }
        })
        tokens.append(center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { _ in
            Task { @MainActor in onWake() }
        })
    }

    deinit {
        for token in tokens {
            NSWorkspace.shared.notificationCenter.removeObserver(token)
        }
    }
}
