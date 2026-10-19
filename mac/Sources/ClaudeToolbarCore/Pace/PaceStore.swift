import Foundation

/// Keeps the learned pace across restarts. A file it cannot use is ignored: the pace is learned again.
public struct PaceStore: Sendable {
    public let path: String

    public init(path: String) {
        self.path = path
    }

    public static func defaultPath() -> String {
        let fm = FileManager.default
        let base = fm.urls(for: .applicationSupportDirectory, in: .userDomainMask).first ?? fm.homeDirectoryForCurrentUser
        return base.appendingPathComponent("ClaudeToolbar", isDirectory: true).appendingPathComponent("pace.json").path
    }

    public func load() -> PaceState? {
        guard let data = FileManager.default.contents(atPath: path),
              let state = try? JSONDecoder().decode(PaceState.self, from: data),
              state.version == 1 else { return nil }
        return state
    }

    public func save(_ state: PaceState) throws {
        let url = URL(fileURLWithPath: path)
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try JSONEncoder().encode(state).write(to: url, options: .atomic)
    }
}
