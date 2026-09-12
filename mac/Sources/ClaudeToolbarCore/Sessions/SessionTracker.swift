import Foundation

public enum SessionState: Sendable { case idle, working, needsAttention, finished, failed }

public enum SessionCue: Sendable { case finished, failed, attention }

public struct SessionInfo: Equatable, Sendable {
    public let id: String
    public let name: String
    public let state: SessionState
    public let updatedAt: Date
    public let message: String?
    /// The window-owning process that hosts this session, once a hook has been traced to it.
    public let host: SessionHost?
    /// How many background agents the last Stop left running; it is what keeps a session working after its turn ended.
    public let agents: Int

    public init(id: String, name: String, state: SessionState, updatedAt: Date, message: String?, host: SessionHost?, agents: Int = 0) {
        self.id = id
        self.name = name
        self.state = state
        self.updatedAt = updatedAt
        self.message = message
        self.host = host
        self.agents = agents
    }
}

/// Per-session state driven by hook events. Not thread-safe: the host calls it on the main actor.
public final class SessionTracker {
    public static let staleAfter: TimeInterval = 12 * 3600

    private var sessions_: [String: SessionInfo] = [:]

    public init() {}

    /// Newest first.
    public var sessions: [SessionInfo] {
        sessions_.values.sorted { a, b in a.updatedAt == b.updatedAt ? a.id < b.id : a.updatedAt > b.updatedAt }
    }

    public func apply(_ e: SessionEvent, now: Date, host: SessionHost? = nil) -> SessionCue? {
        let existing = sessions_[e.sessionId]
        let name = Self.name(for: e, existing: existing)
        // The newest resolved host wins; an event without one keeps what was known.
        let known = host ?? existing?.host
        // Background agents outlive prompts and notifications; only a Stop reports the current number.
        let agents = existing?.agents ?? 0
        switch e.kind {
        case .ended:
            sessions_.removeValue(forKey: e.sessionId)
            return nil
        case .start, .info:
            set(e.sessionId, name, existing?.state ?? .idle, now, existing?.message, known, agents)
            return nil
        case .promptSubmitted:
            set(e.sessionId, name, .working, now, nil, known, agents)
            return nil
        case .needsAttention:
            let alreadyWaiting = existing?.state == .needsAttention
            set(e.sessionId, name, .needsAttention, now, e.message, known, agents)
            return alreadyWaiting ? nil : .attention
        case .stopped:
            if e.runningAgents > 0 {
                // The turn ended but agents it launched are still working: the session is not done and
                // nobody needs the user yet. The conclusion arrives with a later Stop that lists none.
                set(e.sessionId, name, .working, now, nil, known, e.runningAgents)
                return nil
            }
            let alreadyFinished = existing?.state == .finished
            set(e.sessionId, name, .finished, now, nil, known, 0)
            return alreadyFinished ? nil : .finished
        case .failed:
            set(e.sessionId, name, .failed, now, e.detail, known, agents)
            return .failed
        }
    }

    public func session(id: String) -> SessionInfo? { sessions_[id] }

    /// The session a click on Clawd goes to: most urgent state, newest wins ties; sessions without a host cannot be jumped to.
    public var jumpCandidate: SessionInfo? {
        sessions_.values
            .filter { $0.host != nil }
            .sorted { a, b in
                let ra = Self.jumpRank(a.state), rb = Self.jumpRank(b.state)
                if ra != rb { return ra > rb }
                if a.updatedAt != b.updatedAt { return a.updatedAt > b.updatedAt }
                return a.id < b.id
            }
            .first
    }

    private static func jumpRank(_ state: SessionState) -> Int {
        switch state {
        case .needsAttention: return 4
        case .failed: return 3
        case .finished: return 2
        case .working: return 1
        case .idle: return 0
        }
    }

    /// The user looked: finished and failed sessions go idle, waiting ones are assumed handled.
    public func acknowledge() {
        for (id, s) in sessions_ {
            let state: SessionState
            switch s.state {
            case .finished, .failed: state = .idle
            case .needsAttention: state = .working
            default: state = s.state
            }
            if state != s.state || s.message != nil {
                sessions_[id] = SessionInfo(id: s.id, name: s.name, state: state, updatedAt: s.updatedAt, message: nil, host: s.host, agents: s.agents)
            }
        }
    }

    public func prune(now: Date) {
        sessions_ = sessions_.filter { now.timeIntervalSince($0.value.updatedAt) <= Self.staleAfter }
    }

    public var badge: MascotBadge {
        sessions_.values.map { s -> MascotBadge in
            switch s.state {
            case .needsAttention: return .attention
            case .failed: return .failed
            case .finished: return .finished
            case .working: return .working
            case .idle: return .none
            }
        }.max() ?? .none
    }

    public var summary: String? {
        if sessions_.isEmpty { return nil }
        let all = sessions
        var parts: [String] = []
        addGroup(&parts, all, .needsAttention, "needs you")
        addGroup(&parts, all, .failed, "failed")
        addGroup(&parts, all, .finished, "finished")
        addGroup(&parts, all, .working, "working")
        addGroup(&parts, all, .idle, "idle")
        let head = all.count == 1 ? "1 session" : "\(all.count) sessions"
        return head + " · " + parts.joined(separator: " · ")
    }

    private func addGroup(_ parts: inout [String], _ all: [SessionInfo], _ state: SessionState, _ word: String) {
        let group = all.filter { $0.state == state }
        if group.isEmpty { return }
        let names = group.prefix(2).map(\.name).joined(separator: ", ")
        parts.append(all.count == 1 ? "\(word) (\(names))" : "\(group.count) \(word) (\(names))")
    }

    private func set(_ id: String, _ name: String, _ state: SessionState, _ now: Date, _ message: String?, _ host: SessionHost?, _ agents: Int) {
        sessions_[id] = SessionInfo(id: id, name: name, state: state, updatedAt: now, message: message, host: host, agents: agents)
    }

    static func name(for e: SessionEvent, existing: SessionInfo?) -> String {
        if let cwd = e.cwd?.trimmingCharacters(in: .whitespacesAndNewlines), !cwd.isEmpty {
            var trimmed = cwd
            while trimmed.hasSuffix("/") || trimmed.hasSuffix("\\") { trimmed.removeLast() }
            if let cut = trimmed.lastIndex(where: { $0 == "/" || $0 == "\\" }) {
                let leaf = String(trimmed[trimmed.index(after: cut)...])
                if !leaf.isEmpty { return leaf }
            } else if !trimmed.isEmpty {
                return trimmed
            }
        }
        return existing?.name ?? String(e.sessionId.prefix(8))
    }
}
