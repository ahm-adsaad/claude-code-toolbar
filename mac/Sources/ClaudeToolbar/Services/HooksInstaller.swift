import Foundation
import ClaudeToolbarCore

/// Edits Claude Code's settings.json with a backup next to it. Nothing here runs unless the user asks for it.
enum HooksInstaller {
    static let backupSuffix = ".claudetoolbar-bak"

    static var settingsPath: String { CredentialsPaths.claudeSettingsPathFromEnvironment() }

    static func isInstalled(url: String) -> Bool {
        guard let text = try? String(contentsOfFile: settingsPath, encoding: .utf8) else { return false }
        return (try? HooksConfig.isInstalled(text, url: url)) ?? false
    }

    /// nil on success, otherwise a message for the settings window.
    static func install(url: String) -> String? { edit { try HooksConfig.install($0, url: url) } }

    static func remove(url: String) -> String? { edit { try HooksConfig.remove($0, url: url) } }

    private static func edit(_ change: (String) throws -> String) -> String? {
        let path = settingsPath
        let fm = FileManager.default
        do {
            let current = fm.fileExists(atPath: path) ? try String(contentsOfFile: path, encoding: .utf8) : ""
            if fm.fileExists(atPath: path) {
                try? fm.removeItem(atPath: path + backupSuffix)
                try fm.copyItem(atPath: path, toPath: path + backupSuffix)
            }
            let updated = try change(current)
            try fm.createDirectory(atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
            try Data(updated.utf8).write(to: URL(fileURLWithPath: path), options: .atomic)
            Log.info("Claude Code hooks updated in \(path)")
            return nil
        } catch HooksConfig.Error.invalidJSON {
            Log.error("\(path) is not valid JSON; hooks left alone")
            return "\(path) is not valid JSON"
        } catch {
            Log.error("Could not update \(path): \(error.localizedDescription)")
            return error.localizedDescription
        }
    }
}
