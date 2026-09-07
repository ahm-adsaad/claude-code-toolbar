import Foundation

/// Rolling file log at ~/Library/Logs/ClaudeToolbar/app.log. Never log tokens or credential payloads.
final class Log {
    static let shared = Log()

    private let queue = DispatchQueue(label: "io.github.ahm-adsaad.ClaudeToolbar.log")
    private let path: String
    private let maxBytes = 1_000_000

    private static let timestampFormatter: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime]
        return f
    }()

    private init() {
        let directory = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Logs/ClaudeToolbar", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        path = directory.appendingPathComponent("app.log").path
    }

    static func info(_ message: String) { shared.write("INFO", message) }
    static func error(_ message: String) { shared.write("ERROR", message) }
    static func flush() { shared.queue.sync {} }

    private func write(_ level: String, _ message: String) {
        let line = "\(Self.timestampFormatter.string(from: Date())) [\(level)] \(message)\n"
        queue.async {
            self.rotateIfNeeded()
            if let handle = FileHandle(forWritingAtPath: self.path) {
                _ = try? handle.seekToEnd()
                try? handle.write(contentsOf: Data(line.utf8))
                try? handle.close()
            } else {
                FileManager.default.createFile(atPath: self.path, contents: Data(line.utf8))
            }
        }
    }

    private func rotateIfNeeded() {
        guard let attributes = try? FileManager.default.attributesOfItem(atPath: path),
              let size = attributes[.size] as? Int, size > maxBytes else { return }
        let backup = path + ".1"
        try? FileManager.default.removeItem(atPath: backup)
        try? FileManager.default.moveItem(atPath: path, toPath: backup)
    }
}
