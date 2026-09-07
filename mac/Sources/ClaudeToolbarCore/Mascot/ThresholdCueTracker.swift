import Foundation

public struct TrackedRow: Equatable, Sendable {
    public let key: String
    public let level: BarLevel
    public let resetsAt: Date?

    public init(key: String, level: BarLevel, resetsAt: Date?) {
        self.key = key
        self.level = level
        self.resetsAt = resetsAt
    }
}

extension BarLevel {
    /// ok < warn < crit.
    var rank: Int {
        switch self {
        case .ok: return 0
        case .warn: return 1
        case .crit: return 2
        }
    }
}

public enum TrackedRows {
    /// Pairs each visible row with the reset date of the window it shows, keyed by the row label.
    public static func from(_ model: StatusItemModel, snapshot: UsageSnapshot?) -> [TrackedRow] {
        model.rows.map { row in
            let window: UsageWindow?
            switch row.label {
            case "5h": window = snapshot?.fiveHour
            case "7d": window = snapshot?.sevenDay
            case "7d Opus": window = snapshot?.sevenDayOpus
            case "7d Sonnet": window = snapshot?.sevenDaySonnet
            default: window = nil
            }
            return TrackedRow(key: row.label, level: row.level, resetsAt: window?.resetsAt)
        }
    }
}

/// Emits a cue when a row's level rises within the same reset period. The first observation of a key only primes it.
public final class ThresholdCueTracker {
    private var last: [String: TrackedRow] = [:]

    public init() {}

    public func observe(_ rows: [TrackedRow]) -> MascotCue? {
        var cue: MascotCue?
        var seen = Set<String>()
        for row in rows {
            seen.insert(row.key)
            if let previous = last[row.key], previous.resetsAt == row.resetsAt, row.level.rank > previous.level.rank {
                let candidate: MascotCue = row.level == .crit ? .crit : .warn
                if let current = cue {
                    if candidate > current { cue = candidate }
                } else {
                    cue = candidate
                }
            }
            last[row.key] = row
        }
        last = last.filter { seen.contains($0.key) }
        return cue
    }

    public func reset() {
        last.removeAll()
    }
}
