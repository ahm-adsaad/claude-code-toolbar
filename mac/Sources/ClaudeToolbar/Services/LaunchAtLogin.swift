import Foundation
import ServiceManagement

enum LaunchAtLogin {
    /// `SMAppService` needs a real app bundle; a bare binary from `swift build` has no bundle identifier.
    static var isAvailable: Bool { Bundle.main.bundleIdentifier != nil }

    static var isEnabled: Bool {
        guard isAvailable else { return false }
        return SMAppService.mainApp.status == .enabled
    }

    static var statusDescription: String {
        guard isAvailable else { return "unavailable outside an app bundle" }
        switch SMAppService.mainApp.status {
        case .enabled: return "enabled"
        case .notRegistered: return "off"
        case .requiresApproval: return "waiting for approval in System Settings › General › Login Items"
        case .notFound: return "not found"
        @unknown default: return "unknown"
        }
    }

    @discardableResult
    static func apply(_ enabled: Bool) -> Bool {
        guard isAvailable else { return false }
        do {
            let status = SMAppService.mainApp.status
            if enabled, status != .enabled {
                try SMAppService.mainApp.register()
            } else if !enabled, status == .enabled {
                try SMAppService.mainApp.unregister()
            }
            return true
        } catch {
            Log.error("Launch at login change failed: \(error.localizedDescription)")
            return false
        }
    }
}
