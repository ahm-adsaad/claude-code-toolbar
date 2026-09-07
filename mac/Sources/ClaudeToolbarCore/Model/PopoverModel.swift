import Foundation

public struct PopoverRow: Equatable, Sendable {
    public let name: String
    public let utilization: Double
    public let percentText: String
    public let level: BarLevel
    public let resetText: String?
    public let resetClockText: String?

    public init(name: String, utilization: Double, percentText: String, level: BarLevel, resetText: String?, resetClockText: String?) {
        self.name = name
        self.utilization = utilization
        self.percentText = percentText
        self.level = level
        self.resetText = resetText
        self.resetClockText = resetClockText
    }
}

public struct PopoverModel: Equatable, Sendable {
    public let rows: [PopoverRow]
    public let updatedText: String?
    public let statusText: String

    public init(rows: [PopoverRow], updatedText: String?, statusText: String) {
        self.rows = rows
        self.updatedText = updatedText
        self.statusText = statusText
    }
}

public enum PopoverModelBuilder {
    public static func build(state: MonitorState, settings: AppSettings, now: Date, formatClock: (Date) -> String) -> PopoverModel {
        var rows: [PopoverRow] = []
        let a = settings.appearance

        func add(_ name: String, _ window: UsageWindow?) {
            guard let window else { return }
            rows.append(PopoverRow(
                name: name,
                utilization: window.utilization,
                percentText: PercentFormatter.format(window.utilization),
                level: BarLevelResolver.resolve(utilization: window.utilization, warnThreshold: a.warnThreshold, critThreshold: a.critThreshold),
                resetText: window.resetsAt.map { "resets in " + RemainingTimeFormatter.format(resetsAt: $0, now: now) },
                resetClockText: window.resetsAt.map { "at " + formatClock($0) }))
        }

        if let s = state.snapshot {
            add("Session", s.fiveHour)
            add("Weekly", s.sevenDay)
            add("Weekly Opus", s.sevenDayOpus)
            add("Weekly Sonnet", s.sevenDaySonnet)
        }

        let updated = state.lastSuccess.map { "Updated \(AgoFormatter.format(ago: now.timeIntervalSince($0))) ago" }

        let status: String
        switch state.status {
        case .ok: status = "OK"
        case .stale: status = "Stale: \(state.message ?? "no connection")"
        case .expired: status = "Login expired — run claude"
        case .noCredentials: status = "Not signed in — run claude"
        case .loading: status = "Loading…"
        }

        return PopoverModel(rows: rows, updatedText: updated, statusText: status)
    }
}
