import Foundation

public enum UsageStatus: Sendable, Equatable {
    case loading
    case ok
    case stale
    case expired
    case noCredentials
}

public struct MonitorState: Equatable, Sendable {
    public var status: UsageStatus
    public var snapshot: UsageSnapshot?
    public var lastSuccess: Date?
    public var message: String?
    public var credentials: CredentialsState

    public init(status: UsageStatus, snapshot: UsageSnapshot?, lastSuccess: Date?, message: String?, credentials: CredentialsState) {
        self.status = status
        self.snapshot = snapshot
        self.lastSuccess = lastSuccess
        self.message = message
        self.credentials = credentials
    }

    public static func initial(credentials: CredentialsState) -> MonitorState {
        MonitorState(status: .loading, snapshot: nil, lastSuccess: nil, message: nil, credentials: credentials)
    }
}
