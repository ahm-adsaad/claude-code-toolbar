import Foundation

public struct StatusRow: Equatable, Sendable {
    public let label: String
    public let utilization: Double
    public let percentText: String
    public let timeText: String
    public let level: BarLevel
    public let hasData: Bool

    public init(label: String, utilization: Double, percentText: String, timeText: String, level: BarLevel, hasData: Bool) {
        self.label = label
        self.utilization = utilization
        self.percentText = percentText
        self.timeText = timeText
        self.level = level
        self.hasData = hasData
    }
}

public struct StatusItemModel: Equatable, Sendable {
    public let rows: [StatusRow]
    /// Render at 50 % alpha (expired login).
    public let dimmed: Bool
    public let showStaleDot: Bool
    /// Replaces the rows entirely when set (no credentials / no rows enabled).
    public let notice: String?
    /// Drawn after the first row when set (expired login); replaces that row's time text.
    public let hint: String?

    public init(rows: [StatusRow], dimmed: Bool, showStaleDot: Bool, notice: String?, hint: String?) {
        self.rows = rows
        self.dimmed = dimmed
        self.showStaleDot = showStaleDot
        self.notice = notice
        self.hint = hint
    }
}

public enum StatusItemModelBuilder {
    public static let signInNotice = "Sign in with claude"
    public static let runClaudeHint = "↻ run claude"
    public static let noRowsNotice = "No rows enabled"
    public static let placeholder = "—"

    public static func build(state: MonitorState, settings: AppSettings, now: Date) -> StatusItemModel {
        if state.status == .noCredentials {
            return StatusItemModel(rows: [], dimmed: false, showStaleDot: false, notice: signInNotice, hint: nil)
        }

        let appearance = settings.appearance
        let r = settings.rows
        var rows: [StatusRow] = []

        func add(_ show: Bool, _ label: String, _ window: UsageWindow?) {
            guard show else { return }
            guard let window else {
                rows.append(StatusRow(label: label, utilization: 0, percentText: placeholder, timeText: "", level: .ok, hasData: false))
                return
            }
            let time = window.resetsAt.map { RemainingTimeFormatter.format(resetsAt: $0, now: now) } ?? ""
            rows.append(StatusRow(
                label: label,
                utilization: window.utilization,
                percentText: PercentFormatter.format(window.utilization),
                timeText: time,
                level: BarLevelResolver.resolve(utilization: window.utilization, warnThreshold: appearance.warnThreshold, critThreshold: appearance.critThreshold),
                hasData: true))
        }

        add(r.showFiveHour, "5h", state.snapshot?.fiveHour)
        add(r.showSevenDay, "7d", state.snapshot?.sevenDay)
        add(r.showSevenDayOpus, "7d Opus", state.snapshot?.sevenDayOpus)
        add(r.showSevenDaySonnet, "7d Sonnet", state.snapshot?.sevenDaySonnet)

        let expired = state.status == .expired
        return StatusItemModel(
            rows: rows,
            dimmed: expired,
            showStaleDot: state.status == .stale,
            notice: rows.isEmpty ? noRowsNotice : nil,
            hint: expired && !rows.isEmpty ? runClaudeHint : nil)
    }
}
