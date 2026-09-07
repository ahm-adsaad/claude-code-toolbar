import Foundation
import ClaudeToolbarCore

/// Fixed data for the settings preview and the --snapshot mode. Never touches the network or Keychain.
enum SampleData {
    static let now = Date(timeIntervalSince1970: 1_788_782_400) // 2026-09-07T12:00:00Z

    static let credentials = CredentialsState.valid(
        source: "Keychain", accessToken: "sample", expiresAt: now.addingTimeInterval(6 * 3600), subscriptionType: "max")

    static var snapshot: UsageSnapshot {
        UsageSnapshot(
            fiveHour: UsageWindow(utilization: 42, resetsAt: now.addingTimeInterval(2 * 3600 + 13 * 60)),
            sevenDay: UsageWindow(utilization: 18, resetsAt: now.addingTimeInterval(3 * 86_400 + 4 * 3600)),
            sevenDayOpus: nil,
            sevenDaySonnet: nil,
            fetchedAt: now.addingTimeInterval(-12))
    }

    static var fullSnapshot: UsageSnapshot {
        UsageSnapshot(
            fiveHour: UsageWindow(utilization: 42, resetsAt: now.addingTimeInterval(2 * 3600 + 13 * 60)),
            sevenDay: UsageWindow(utilization: 18, resetsAt: now.addingTimeInterval(3 * 86_400 + 4 * 3600)),
            sevenDayOpus: UsageWindow(utilization: 75, resetsAt: now.addingTimeInterval(3 * 86_400 + 4 * 3600)),
            sevenDaySonnet: UsageWindow(utilization: 96, resetsAt: now.addingTimeInterval(86_400 + 2 * 3600)),
            fetchedAt: now.addingTimeInterval(-12))
    }

    static func state(_ status: UsageStatus, snapshot: UsageSnapshot? = SampleData.snapshot) -> MonitorState {
        switch status {
        case .ok:
            return MonitorState(status: .ok, snapshot: snapshot, lastSuccess: snapshot?.fetchedAt, message: nil, credentials: credentials)
        case .stale:
            return MonitorState(status: .stale, snapshot: snapshot, lastSuccess: now.addingTimeInterval(-180), message: "The network connection was lost", credentials: credentials)
        case .expired:
            return MonitorState(status: .expired, snapshot: snapshot, lastSuccess: now.addingTimeInterval(-3600), message: "Login expired",
                                credentials: .expired(source: "Keychain", expiresAt: now.addingTimeInterval(-600), subscriptionType: "max"))
        case .noCredentials:
            return MonitorState(status: .noCredentials, snapshot: nil, lastSuccess: nil, message: "Credentials not found", credentials: .missing(source: "Keychain"))
        case .loading:
            return MonitorState(status: .loading, snapshot: nil, lastSuccess: nil, message: nil, credentials: credentials)
        }
    }
}
