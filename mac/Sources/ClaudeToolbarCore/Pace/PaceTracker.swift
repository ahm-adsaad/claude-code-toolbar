import Foundation

/// Learns how fast each limit rises per minute of Claude working, and from that how much working time is left
/// before it runs out. Not thread-safe: the host calls it on the main actor.
public final class PaceTracker {
    /// The smallest rise in points the rate is measured over; below it the estimate is still learning.
    public static let minRise: Double = 3

    private static let maxPoints = 200
    private static let resetShift: TimeInterval = 10 * 60
    private static let resetDrop: Double = 5

    private var state: PaceState
    private var last: Date?
    private var count = 0

    public init(restored: PaceState? = nil) {
        state = restored ?? PaceState()
    }

    /// How much recent working time the rate is measured over, when there is that much.
    public static func lookback(_ limit: UsageLimit) -> TimeInterval {
        limit == .fiveHour ? 30 * 60 : 8 * 3600
    }

    /// Counts the time since the last call at the number of sessions that were working then.
    public func advance(workingSessions: Int, now: Date) {
        if let last {
            if now <= last {
                // A clock set back adds nothing; keeping the later time avoids counting the same minutes twice.
                count = workingSessions
                return
            }
            if count > 0 {
                let minutes = now.timeIntervalSince(last) / 60
                state.workMinutes += minutes * Double(count)
                state.activeMinutes += minutes
            }
        }
        last = now
        count = workingSessions
    }

    /// The machine is going to sleep: count up to now, then nothing until the next `advance`.
    public func suspend(now: Date) {
        advance(workingSessions: count, now: now)
        last = nil
    }

    public func record(_ snapshot: UsageSnapshot, workingSessions: Int) {
        advance(workingSessions: workingSessions, now: snapshot.fetchedAt)
        for limit in UsageLimit.allCases {
            guard let window = snapshot.window(limit) else { continue }
            let point = PacePoint(at: snapshot.fetchedAt, workMinutes: state.workMinutes, activeMinutes: state.activeMinutes, utilization: window.utilization)
            var history = state.limits[limit] ?? PaceLimitHistory()
            if Self.isNewPeriod(history, window) { history = PaceLimitHistory() }
            history.resetsAt = window.resetsAt
            if history.points.last?.utilization != window.utilization {
                history.points.append(point)
                if history.points.count > Self.maxPoints { history.points.removeFirst(history.points.count - Self.maxPoints) }
            }
            history.latest = point
            state.limits[limit] = history
        }
    }

    private static func isNewPeriod(_ history: PaceLimitHistory, _ window: UsageWindow) -> Bool {
        if let before = history.resetsAt, let after = window.resetsAt, after.timeIntervalSince(before) > resetShift { return true }
        if let latest = history.latest, latest.utilization - window.utilization >= resetDrop { return true }
        return false
    }

    public func estimate(_ limit: UsageLimit, now: Date, workingSessions: Int) -> PaceEstimate {
        guard let history = state.limits[limit], let cur = history.latest else { return .learning }
        if cur.utilization >= 100 {
            return PaceEstimate(verdict: .usedUp, workLeft: 0, workLeftNow: nil, sessionsNow: workingSessions, span: 0, basis: .work)
        }

        // Measured from the newest reading that is far enough back in working time, else from the oldest that shows a
        // rise at all.
        let lookback = Self.lookback(limit) / 60
        var oldest: PacePoint?
        var newest: PacePoint?
        for p in history.points where cur.utilization - p.utilization >= Self.minRise {
            if oldest == nil { oldest = p }
            if cur.activeMinutes - p.activeMinutes >= lookback { newest = p }
        }
        guard let oldest else { return .learning }
        let start = newest ?? oldest

        let dU = cur.utilization - start.utilization
        let dW = cur.workMinutes - start.workMinutes
        let dA = cur.activeMinutes - start.activeMinutes
        let dT = cur.at.timeIntervalSince(start.at) / 60
        let toReset = history.resetsAt.map { $0.timeIntervalSince(now) / 60 }
        let left = 100 - cur.utilization

        let workLeft: Double
        let available: Double?
        var workLeftNow: Double?
        let basis: PaceBasis
        let span: Double
        if dW > 0 {
            let sessionMinutesLeft = left / (dU / dW)
            // Minutes in which several sessions worked count once each, so the answer is in the user's own time.
            workLeft = sessionMinutesLeft / max(1, dW / dA)
            if workingSessions >= 2 { workLeftNow = sessionMinutesLeft / Double(workingSessions) }
            // Only the share of the clock that usually has Claude working can be spent before the reset.
            let share = dT > 0 ? min(max(dA / dT, 0), 1) : 1
            available = toReset.map { share * $0 }
            basis = .work
            span = dA
        } else {
            if dT <= 0 { return .learning }
            workLeft = left / (dU / dT)
            available = toReset
            basis = .clock
            span = dT
        }

        let verdict: PaceVerdict = available.map { workLeft < $0 } == true ? .runsOutFirst : .lasts
        return PaceEstimate(verdict: verdict, workLeft: Self.seconds(workLeft), workLeftNow: workLeftNow.map(Self.seconds),
                            sessionsNow: workingSessions, span: Self.seconds(span), basis: basis)
    }

    public func export() -> PaceState { state }

    // Whole seconds: an estimate has no use for fractions, and it keeps 6h 30m exact.
    private static func seconds(_ minutes: Double) -> TimeInterval { (minutes * 60).rounded() }
}
