import Foundation

/// Adds or removes the toolbar's HTTP hooks in a Claude Code settings document. Other content is preserved (keys are re-sorted on write).
public enum HooksConfig {
    public enum Error: Swift.Error { case invalidJSON }

    public static let events = ["SessionStart", "UserPromptSubmit", "Notification", "Stop", "StopFailure", "SessionEnd"]
    public static let hookTimeoutSeconds = 5

    public static func hookUrl(port: Int) -> String { "http://127.0.0.1:\(port)/hook" }

    public static func isInstalled(_ json: String, url: String) throws -> Bool {
        let root = try parseObject(json)
        guard let hooks = root["hooks"] as? [String: Any] else { return false }
        return events.allSatisfy { event in
            (hooks[event] as? [[String: Any]])?.contains { hasHandler($0, url: url) } ?? false
        }
    }

    public static func install(_ json: String, url: String) throws -> String {
        var root = try parseObject(json)
        var hooks = root["hooks"] as? [String: Any] ?? [:]
        for event in events {
            var groups = hooks[event] as? [[String: Any]] ?? []
            if !groups.contains(where: { hasHandler($0, url: url) }) {
                groups.append(["hooks": [["type": "http", "url": url, "timeout": hookTimeoutSeconds]]])
            }
            hooks[event] = groups
        }
        root["hooks"] = hooks
        return try serialize(root)
    }

    public static func remove(_ json: String, url: String) throws -> String {
        var root = try parseObject(json)
        if var hooks = root["hooks"] as? [String: Any] {
            for event in events {
                guard var groups = hooks[event] as? [[String: Any]] else { continue }
                groups = groups.compactMap { group -> [String: Any]? in
                    var group = group
                    guard let handlers = group["hooks"] as? [[String: Any]] else { return group }
                    let kept = handlers.filter { !isOurs($0, url: url) }
                    if kept.isEmpty { return nil }
                    group["hooks"] = kept
                    return group
                }
                if groups.isEmpty { hooks.removeValue(forKey: event) } else { hooks[event] = groups }
            }
            if hooks.isEmpty { root.removeValue(forKey: "hooks") } else { root["hooks"] = hooks }
        }
        return try serialize(root)
    }

    private static func hasHandler(_ group: [String: Any], url: String) -> Bool {
        (group["hooks"] as? [[String: Any]])?.contains { isOurs($0, url: url) } ?? false
    }

    private static func isOurs(_ handler: [String: Any], url: String) -> Bool {
        handler["type"] as? String == "http" && handler["url"] as? String == url
    }

    private static func parseObject(_ json: String) throws -> [String: Any] {
        let trimmed = json.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty { return [:] }
        guard let object = try? JSONSerialization.jsonObject(with: Data(trimmed.utf8), options: [.fragmentsAllowed]),
              let root = object as? [String: Any] else { throw Error.invalidJSON }
        return root
    }

    private static func serialize(_ root: [String: Any]) throws -> String {
        let data = try JSONSerialization.data(withJSONObject: root, options: [.prettyPrinted, .sortedKeys])
        return String(decoding: data, as: UTF8.self)
    }
}
