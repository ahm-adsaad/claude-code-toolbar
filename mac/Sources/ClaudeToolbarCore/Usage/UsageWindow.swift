import Foundation

public struct UsageWindow: Equatable, Sendable {
    /// 0...100, clamped by the parser.
    public let utilization: Double
    public let resetsAt: Date?

    public init(utilization: Double, resetsAt: Date?) {
        self.utilization = utilization
        self.resetsAt = resetsAt
    }
}
