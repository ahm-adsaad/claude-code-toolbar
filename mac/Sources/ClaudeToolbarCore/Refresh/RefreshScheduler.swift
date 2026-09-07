import Foundation

/// Decides when the next usage fetch is due. Owned and used only by `UsageMonitor`.
public final class RefreshScheduler {
    public static let initialBackoff: TimeInterval = 15
    public static let maxBackoff: TimeInterval = 300
    static let resetGrace: TimeInterval = 1

    private let clock: any ClockSource

    public var intervalSeconds: Int
    public private(set) var nextDue: Date?
    public private(set) var currentBackoff: TimeInterval = 0

    public init(clock: any ClockSource, intervalSeconds: Int = 60) {
        self.clock = clock
        self.intervalSeconds = intervalSeconds
        nextDue = clock.now
    }

    public var isPaused: Bool { nextDue == nil }

    public func isDue(at now: Date) -> Bool {
        guard let due = nextDue else { return false }
        return now >= due
    }

    public func requestImmediate() {
        nextDue = clock.now
    }

    public func pause() {
        nextDue = nil
    }

    public func onSuccess(nextReset: Date?) {
        currentBackoff = 0
        let now = clock.now
        var due = now.addingTimeInterval(TimeInterval(intervalSeconds))
        if let reset = nextReset {
            let afterReset = reset.addingTimeInterval(Self.resetGrace)
            if afterReset > now && afterReset < due { due = afterReset }
        }
        nextDue = due
    }

    public func onFailure(retryAfter: TimeInterval?) {
        currentBackoff = currentBackoff == 0 ? Self.initialBackoff : min(currentBackoff * 2, Self.maxBackoff)
        var delay = currentBackoff
        if let retryAfter, retryAfter > delay { delay = retryAfter }
        nextDue = clock.now.addingTimeInterval(delay)
    }
}
