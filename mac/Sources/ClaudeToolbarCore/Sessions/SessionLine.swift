import Foundation

/// One popover line per session: "api · needs you · VS Code · 2 min", or "api · working · 2 agents · VS Code · now".
public enum SessionLine {
    public static func text(_ s: SessionInfo, now: Date) -> String {
        var parts = [s.name, stateWord(s.state)]
        if s.state == .working && s.agents > 0 { parts.append(s.agents == 1 ? "1 agent" : "\(s.agents) agents") }
        if let host = s.host { parts.append(host.name) }
        parts.append(age(now.timeIntervalSince(s.updatedAt)))
        return parts.joined(separator: " · ")
    }

    public static func stateWord(_ state: SessionState) -> String {
        switch state {
        case .needsAttention: return "needs you"
        case .failed: return "failed"
        case .finished: return "finished"
        case .working: return "working"
        case .idle: return "idle"
        }
    }

    public static func age(_ seconds: TimeInterval) -> String {
        if seconds < 60 { return "now" }
        if seconds < 3600 { return "\(Int(seconds / 60)) min" }
        return "\(Int(seconds / 3600)) h"
    }
}
