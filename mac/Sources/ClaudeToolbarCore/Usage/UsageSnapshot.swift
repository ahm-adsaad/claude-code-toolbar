import Foundation

public struct UsageSnapshot: Equatable, Sendable {
    public let fiveHour: UsageWindow?
    public let sevenDay: UsageWindow?
    public let sevenDayOpus: UsageWindow?
    public let sevenDaySonnet: UsageWindow?
    public let fetchedAt: Date

    public init(fiveHour: UsageWindow?, sevenDay: UsageWindow?, sevenDayOpus: UsageWindow?, sevenDaySonnet: UsageWindow?, fetchedAt: Date) {
        self.fiveHour = fiveHour
        self.sevenDay = sevenDay
        self.sevenDayOpus = sevenDayOpus
        self.sevenDaySonnet = sevenDaySonnet
        self.fetchedAt = fetchedAt
    }

    public var windows: [UsageWindow] {
        [fiveHour, sevenDay, sevenDayOpus, sevenDaySonnet].compactMap { $0 }
    }

    public var nextReset: Date? {
        windows.compactMap(\.resetsAt).min()
    }
}
