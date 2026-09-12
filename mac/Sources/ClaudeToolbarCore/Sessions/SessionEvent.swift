import Foundation

public enum SessionEventKind: Sendable { case start, promptSubmitted, needsAttention, info, stopped, failed, ended }

public struct SessionEvent: Equatable, Sendable {
    public let kind: SessionEventKind
    public let sessionId: String
    public let cwd: String?
    public let message: String?
    public let detail: String?
    /// Only ever non-zero on a stopped event: the number of background agents the turn left running,
    /// taken from the Stop payload's `background_tasks`.
    public let runningAgents: Int

    public init(kind: SessionEventKind, sessionId: String, cwd: String?, message: String?, detail: String?, runningAgents: Int = 0) {
        self.kind = kind
        self.sessionId = sessionId
        self.cwd = cwd
        self.message = message
        self.detail = detail
        self.runningAgents = runningAgents
    }
}

/// Turns a Claude Code hook payload into a session event. Anything we do not track returns nil.
public enum HookEventParser {
    // Not "idle_prompt": Claude Code sends it a minute after every turn ends ("Claude is waiting for your input"),
    // which is the finished cue again with a false "needs you" attached.
    private static let attentionTypes: Set<String> = ["permission_prompt", "worker_permission_prompt", "agent_needs_input", "elicitation_dialog", "elicitation_url_dialog"]

    /// Background task types that are agents whose results the session is still waiting for. A dev server is not one.
    private static let agentTaskTypes: Set<String> = ["subagent", "local_agent", "remote_agent", "in_process_teammate"]
    private static let runningStatuses: Set<String> = ["running", "pending"]

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
            return SessionEvent(kind: .stopped, sessionId: id, cwd: cwd, message: nil, detail: nil, runningAgents: runningAgents(root))
        case "StopFailure":
            return SessionEvent(kind: .failed, sessionId: id, cwd: cwd, message: nil, detail: root["error_type"] as? String)
        case "SessionEnd":
            return SessionEvent(kind: .ended, sessionId: id, cwd: cwd, message: nil, detail: root["reason"] as? String)
        default:
            return nil
        }
    }

    /// Stop fires when the main agent's turn ends, even while agents it launched in the background are still
    /// working; the payload lists them. Counting them here is what lets the tracker hold "finished" back.
    private static func runningAgents(_ root: [String: Any]) -> Int {
        guard let tasks = root["background_tasks"] as? [Any] else { return 0 }
        return tasks.reduce(0) { count, task in
            guard let task = task as? [String: Any],
                  let type = task["type"] as? String, agentTaskTypes.contains(type),
                  let status = task["status"] as? String, runningStatuses.contains(status) else { return count }
            return count + 1
        }
    }
}
