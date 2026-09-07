import Foundation

public enum SessionEventKind: Sendable { case start, promptSubmitted, needsAttention, info, stopped, failed, ended }

public struct SessionEvent: Equatable, Sendable {
    public let kind: SessionEventKind
    public let sessionId: String
    public let cwd: String?
    public let message: String?
    public let detail: String?

    public init(kind: SessionEventKind, sessionId: String, cwd: String?, message: String?, detail: String?) {
        self.kind = kind
        self.sessionId = sessionId
        self.cwd = cwd
        self.message = message
        self.detail = detail
    }
}

/// Turns a Claude Code hook payload into a session event. Anything we do not track returns nil.
public enum HookEventParser {
    private static let attentionTypes: Set<String> = ["permission_prompt", "idle_prompt", "agent_needs_input", "elicitation_dialog", "elicitation_url_dialog"]

    public static func parse(_ json: String) -> SessionEvent? {
        let trimmed = json.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty,
              let object = try? JSONSerialization.jsonObject(with: Data(trimmed.utf8), options: [.fragmentsAllowed]),
              let root = object as? [String: Any],
              let name = root["hook_event_name"] as? String,
              let id = root["session_id"] as? String, !id.isEmpty else { return nil }
        let cwd = root["cwd"] as? String
        switch name {
        case "SessionStart":
            return SessionEvent(kind: .start, sessionId: id, cwd: cwd, message: nil, detail: root["source"] as? String)
        case "UserPromptSubmit":
            return SessionEvent(kind: .promptSubmitted, sessionId: id, cwd: cwd, message: nil, detail: nil)
        case "Notification":
            let type = root["notification_type"] as? String ?? ""
            return SessionEvent(kind: attentionTypes.contains(type) ? .needsAttention : .info, sessionId: id, cwd: cwd,
                                message: root["message"] as? String, detail: type)
        case "Stop":
            return SessionEvent(kind: .stopped, sessionId: id, cwd: cwd, message: nil, detail: nil)
        case "StopFailure":
            return SessionEvent(kind: .failed, sessionId: id, cwd: cwd, message: nil, detail: root["error_type"] as? String)
        case "SessionEnd":
            return SessionEvent(kind: .ended, sessionId: id, cwd: cwd, message: nil, detail: root["reason"] as? String)
        default:
            return nil
        }
    }
}
