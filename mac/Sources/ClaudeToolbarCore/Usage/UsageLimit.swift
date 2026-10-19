import Foundation

/// The rate limits the usage endpoint reports, one per row. A dictionary key in the pace file, hence `CodingKeyRepresentable`.
public enum UsageLimit: String, CaseIterable, Codable, CodingKeyRepresentable, Sendable {
    case fiveHour, sevenDay, sevenDayOpus, sevenDaySonnet, sevenDayFable
}

extension UsageSnapshot {
    public func window(_ limit: UsageLimit) -> UsageWindow? {
        switch limit {
        case .fiveHour: return fiveHour
        case .sevenDay: return sevenDay
        case .sevenDayOpus: return sevenDayOpus
        case .sevenDaySonnet: return sevenDaySonnet
        case .sevenDayFable: return sevenDayFable
        }
    }
}
