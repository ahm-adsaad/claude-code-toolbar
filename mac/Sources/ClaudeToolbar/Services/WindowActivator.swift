import AppKit
import ApplicationServices
import ClaudeToolbarCore

/// Brings a session's host forward. Activating the app needs no permission; raising the exact
/// window inside it needs Accessibility, which the user grants once in System Settings.
enum WindowActivator {
    enum Outcome: Equatable {
        case activated(raisedWindow: Bool)
        case gone
    }

    static var accessibilityGranted: Bool { AXIsProcessTrusted() }

    @MainActor
    static func activate(host: SessionHost, sessionName: String) -> Outcome {
        guard let app = NSRunningApplication(processIdentifier: pid_t(host.pid)), !app.isTerminated else { return .gone }
        // The status-item click made this app the user's focus, so it may hand activation on.
        NSApp.activate()
        app.activate(from: .current, options: [])
        let raised = accessibilityGranted && raiseWindow(pid: pid_t(host.pid), matching: sessionName)
        return .activated(raisedWindow: raised)
    }

    /// With Accessibility granted: raise the window whose title mentions the session, else the first one.
    private static func raiseWindow(pid: pid_t, matching name: String) -> Bool {
        let app = AXUIElementCreateApplication(pid)
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(app, kAXWindowsAttribute as CFString, &value) == .success,
              let windows = value as? [AXUIElement], !windows.isEmpty else { return false }
        let target = windows.first { window in
            var title: CFTypeRef?
            guard AXUIElementCopyAttributeValue(window, kAXTitleAttribute as CFString, &title) == .success else { return false }
            return (title as? String)?.localizedCaseInsensitiveContains(name) ?? false
        } ?? windows[0]
        _ = AXUIElementSetAttributeValue(target, kAXMainAttribute as CFString, kCFBooleanTrue)
        return AXUIElementPerformAction(target, kAXRaiseAction as CFString) == .success
    }

    static func openAccessibilitySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }
}
