import Foundation

public final class SettingsStore: @unchecked Sendable {
    public let path: String

    public init(path: String) {
        self.path = path
    }

    public static func defaultPath() -> String {
        let fm = FileManager.default
        let base = fm.urls(for: .applicationSupportDirectory, in: .userDomainMask).first ?? fm.homeDirectoryForCurrentUser
        return base.appendingPathComponent("ClaudeToolbar", isDirectory: true).appendingPathComponent("settings.json").path
    }

    public func load() -> AppSettings {
        guard FileManager.default.fileExists(atPath: path) else {
            return SettingsValidator.normalize(AppSettings.createDefault())
        }
        do {
            let text = try String(contentsOfFile: path, encoding: .utf8)
            return SettingsValidator.normalize(try SettingsJSON.decode(text))
        } catch {
            backupBadFile()
            let defaults = SettingsValidator.normalize(AppSettings.createDefault())
            try? save(defaults)
            return defaults
        }
    }

    public func save(_ settings: AppSettings) throws {
        let url = URL(fileURLWithPath: path)
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        let text = try SettingsJSON.encode(SettingsValidator.normalize(settings))
        try Data(text.utf8).write(to: url, options: .atomic)
    }

    private func backupBadFile() {
        let backup = path + ".bad"
        try? FileManager.default.removeItem(atPath: backup)
        try? FileManager.default.copyItem(atPath: path, toPath: backup)
    }
}
