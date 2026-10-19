import Foundation

public enum PaceVerdict: Sendable { case learning, lasts, runsOutFirst, usedUp }

/// What the rate was measured against: time Claude spent working, or the clock when no working time was recorded.
public enum PaceBasis: Sendable { case work, clock }

public struct PaceEstimate: Equatable, Sendable {
    public let verdict: PaceVerdict
    /// How long the limit lasts at the usual pace, in working time.
    public let workLeft: TimeInterval?
    /// How long it lasts with the sessions working right now; only set for two or more.
    public let workLeftNow: TimeInterval?
    public let sessionsNow: Int
    /// How much working (or clock) time the rate was measured over.
    public let span: TimeInterval
    public let basis: PaceBasis

    public init(verdict: PaceVerdict, workLeft: TimeInterval?, workLeftNow: TimeInterval?, sessionsNow: Int, span: TimeInterval, basis: PaceBasis) {
        self.verdict = verdict
        self.workLeft = workLeft
        self.workLeftNow = workLeftNow
        self.sessionsNow = sessionsNow
        self.span = span
        self.basis = basis
    }

    public static let learning = PaceEstimate(verdict: .learning, workLeft: nil, workLeftNow: nil, sessionsNow: 0, span: 0, basis: .work)
}

/// A reading of one limit, with the running totals of working time it was taken at.
public struct PacePoint: Codable, Equatable, Sendable {
    public var at: Date
    public var workMinutes: Double
    public var activeMinutes: Double
    public var utilization: Double

    public init(at: Date, workMinutes: Double, activeMinutes: Double, utilization: Double) {
        self.at = at
        self.workMinutes = workMinutes
        self.activeMinutes = activeMinutes
        self.utilization = utilization
    }
}

/// One limit's readings since its last reset.
public struct PaceLimitHistory: Codable, Equatable, Sendable {
    public var resetsAt: Date?
    /// The readings where utilization changed, oldest first.
    public var points: [PacePoint]
    public var latest: PacePoint?

    public init(resetsAt: Date? = nil, points: [PacePoint] = [], latest: PacePoint? = nil) {
        self.resetsAt = resetsAt
        self.points = points
        self.latest = latest
    }
}

/// Everything the tracker keeps across restarts.
public struct PaceState: Codable, Equatable, Sendable {
    public var version = 1
    /// Session-minutes: a minute with two sessions working counts twice.
    public var workMinutes: Double = 0
    /// Minutes in which at least one session was working.
    public var activeMinutes: Double = 0
    public var limits: [UsageLimit: PaceLimitHistory] = [:]

    public init() {}
}
