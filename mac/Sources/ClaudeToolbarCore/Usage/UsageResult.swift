import Foundation

public enum UsageResult: Equatable, Sendable {
    case ok(UsageSnapshot)
    case unauthorized
    case rateLimited(retryAfter: TimeInterval?)
    case failed(String)
}
