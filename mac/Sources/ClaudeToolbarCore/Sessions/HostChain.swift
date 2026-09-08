import Foundation

/// The window-owning process that hosts a Claude Code session: a terminal, an editor or the desktop app.
public struct SessionHost: Equatable, Sendable {
    public let pid: Int
    public let name: String
    public let resolvedAt: Date

    public init(pid: Int, name: String, resolvedAt: Date) {
        self.pid = pid
        self.name = name
        self.resolvedAt = resolvedAt
    }
}

/// One process as the platform reports it: enough to walk parents and tell hosts apart.
public struct ProcessRecord: Equatable, Sendable {
    public let pid: Int
    public let parentPid: Int
    public let name: String
    public let startTime: Date
    public let hasWindow: Bool

    public init(pid: Int, parentPid: Int, name: String, startTime: Date, hasWindow: Bool) {
        self.pid = pid
        self.parentPid = parentPid
        self.name = name
        self.startTime = startTime
        self.hasWindow = hasWindow
    }
}

/// `known` is true when the name came from the catalog; false when an unknown windowed ancestor was used.
public struct HostChainResult: Equatable, Sendable {
    public let pid: Int
    public let displayName: String
    public let known: Bool

    public init(pid: Int, displayName: String, known: Bool) {
        self.pid = pid
        self.displayName = displayName
        self.known = known
    }
}

/// Per-platform naming: `displayName` maps a process name (Windows: lower-case executable base name;
/// macOS: bundle id or short name) to a display name, or nil; `stopsWalk` marks the shells and system
/// processes the walk must never climb into.
public struct HostCatalog: Sendable {
    public let displayName: @Sendable (String) -> String?
    public let stopsWalk: @Sendable (String) -> Bool

    public init(displayName: @escaping @Sendable (String) -> String?, stopsWalk: @escaping @Sendable (String) -> Bool) {
        self.displayName = displayName
        self.stopsWalk = stopsWalk
    }
}

public enum KnownHosts {
    private static let windowsNames: [String: String] = [
        "windowsterminal": "Windows Terminal",
        "code": "VS Code",
        "code - insiders": "VS Code",
        "cursor": "Cursor",
        "claude": "Claude",
        "idea64": "JetBrains",
        "pycharm64": "JetBrains",
        "webstorm64": "JetBrains",
        "rider64": "JetBrains",
        "goland64": "JetBrains",
        "clion64": "JetBrains",
        "phpstorm64": "JetBrains",
        "rubymine64": "JetBrains",
        "datagrip64": "JetBrains",
        "pwsh": "Console",
        "powershell": "Console",
        "cmd": "Console",
        "mintty": "Git Bash",
        "wezterm-gui": "WezTerm",
        "alacritty": "Alacritty",
        "hyper": "Hyper",
        "tabby": "Tabby",
    ]

    private static let windowsStops: Set<String> = [
        "explorer", "svchost", "services", "wininit", "winlogon", "userinit", "sihost", "runtimebroker",
    ]

    private static let macBundles: [String: String] = [
        "com.apple.Terminal": "Terminal",
        "com.googlecode.iterm2": "iTerm2",
        "com.mitchellh.ghostty": "Ghostty",
        "dev.warp.Warp-Stable": "Warp",
        "net.kovidgoyal.kitty": "kitty",
        "io.alacritty": "Alacritty",
        "com.github.wez.wezterm": "WezTerm",
        "com.microsoft.VSCode": "VS Code",
        "com.microsoft.VSCodeInsiders": "VS Code",
        "com.todesktop.230313mzl4w4u92": "Cursor",
        "com.anthropic.claudefordesktop": "Claude",
    ]

    private static let macStops: Set<String> = ["com.apple.finder", "com.apple.dock", "launchd"]

    public static let windows = HostCatalog(
        displayName: { windowsNames[$0] },
        stopsWalk: { windowsStops.contains($0) })

    public static let mac = HostCatalog(
        displayName: { $0.hasPrefix("com.jetbrains.") ? "JetBrains" : macBundles[$0] },
        stopsWalk: { macStops.contains($0) })
}

/// Walks from the Claude Code process up to the window-owning host. Pure: the platform supplies the table.
public enum HostChain {
    public static let maxDepth = 12

    public static func resolve(startPid: Int, table: [Int: ProcessRecord], catalog: HostCatalog) -> HostChainResult? {
        var windowed: HostChainResult?
        guard var node = table[startPid] else { return nil }
        for _ in 0..<maxDepth {
            if catalog.stopsWalk(node.name) { break }
            if node.hasWindow {
                if let display = catalog.displayName(node.name) {
                    return HostChainResult(pid: node.pid, displayName: display, known: true)
                }
                if windowed == nil { windowed = HostChainResult(pid: node.pid, displayName: node.name, known: false) }
            }
            guard let parent = table[node.parentPid] else { break }
            // A parent that started after its child is a reused pid, not the real parent.
            if parent.pid == node.pid || parent.startTime > node.startTime { break }
            node = parent
        }
        return windowed
    }
}
