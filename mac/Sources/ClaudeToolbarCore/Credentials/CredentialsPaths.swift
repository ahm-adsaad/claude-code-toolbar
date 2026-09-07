import Foundation

public enum CredentialsPaths {
    public static let fileName = ".credentials.json"
    public static let configDirVariable = "CLAUDE_CONFIG_DIR"

    public static func resolve(claudeConfigDir: String?, homeDirectory: String) -> String {
        let override = claudeConfigDir?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let directory = override.isEmpty ? join(homeDirectory, ".claude") : override
        return join(directory, fileName)
    }

    public static func resolveFromEnvironment() -> String {
        resolve(claudeConfigDir: ProcessInfo.processInfo.environment[configDirVariable],
                homeDirectory: NSHomeDirectory())
    }

    public static let settingsFileName = "settings.json"

    /// Claude Code's own settings file, next to the credentials file.
    public static func claudeSettingsPath(claudeConfigDir: String?, homeDirectory: String) -> String {
        let override = claudeConfigDir?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let directory = override.isEmpty ? join(homeDirectory, ".claude") : override
        return join(directory, settingsFileName)
    }

    public static func claudeSettingsPathFromEnvironment() -> String {
        claudeSettingsPath(claudeConfigDir: ProcessInfo.processInfo.environment[configDirVariable], homeDirectory: NSHomeDirectory())
    }

    private static func join(_ directory: String, _ component: String) -> String {
        if directory.hasSuffix("/") || directory.hasSuffix("\\") { return directory + component }
        return directory + "/" + component
    }
}
