import Foundation

public struct StatusRow: Equatable, Sendable {
    public let label: String
    public let utilization: Double
    public let percentText: String
    public let timeText: String
    public let level: BarLevel
    public let hasData: Bool
    /// Set when the limit would run out before its reset at the user's pace; shown even when the time is hidden.
    public let warning: String?
    public let warningLevel: BarLevel

    public init(label: String, utilization: Double, percentText: String, timeText: String, level: BarLevel, hasData: Bool,
                warning: String? = nil, warningLevel: BarLevel = .warn) {
        self.label = label
        self.utilization = utilization
        self.percentText = percentText
        self.timeText = timeText
        self.level = level
        self.hasData = hasData
        self.warning = warning
        self.warningLevel = warningLevel
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
    /// Under this much work left the warning takes the critical colour.
    public static let critWorkLeft: TimeInterval = 30 * 60

    public static func build(state: MonitorState, settings: AppSettings, now: Date, pace: [UsageLimit: PaceEstimate] = [:]) -> StatusItemModel {
        if state.status == .noCredentials {
            return StatusItemModel(rows: [], dimmed: false, showStaleDot: false, notice: signInNotice, hint: nil)
        }

        let appearance = settings.appearance
        let r = settings.rows
        var rows: [StatusRow] = []
        let expired = state.status == .expired

        func add(_ show: Bool, _ limit: UsageLimit, _ label: String, hideWhenMissing: Bool = false) {
            guard show else { return }
            guard let window = state.snapshot?.window(limit) else {
                // Plans without a Fable limit would otherwise carry a permanent "—" row.
                if hideWhenMissing { return }
                rows.append(StatusRow(label: label, utilization: 0, percentText: placeholder, timeText: "", level: .ok, hasData: false))
                return
            }
            let time = window.resetsAt.map { RemainingTimeFormatter.format(resetsAt: $0, now: now) } ?? ""
            // An expired login means the numbers stopped moving; a warning drawn from them would go stale.
            var runsOut: TimeInterval?
            if !expired, let e = pace[limit], e.verdict == .runsOutFirst { runsOut = e.workLeft }
            rows.append(StatusRow(
                label: label,
                utilization: window.utilization,
                percentText: PercentFormatter.format(window.utilization),
                timeText: time,
                level: BarLevelResolver.resolve(utilization: window.utilization, warnThreshold: appearance.warnThreshold, critThreshold: appearance.critThreshold),
                hasData: true,
                warning: runsOut.map { PaceFormatter.format($0) + " work" },
                warningLevel: (runsOut ?? .infinity) < critWorkLeft ? .crit : .warn))
        }

        add(r.showFiveHour, .fiveHour, "5h")
        add(r.showSevenDay, .sevenDay, "7d")
        add(r.showSevenDayOpus, .sevenDayOpus, "7d Opus")
        add(r.showSevenDaySonnet, .sevenDaySonnet, "7d Sonnet")
        add(r.showSevenDayFable, .sevenDayFable, "7d Fable", hideWhenMissing: true)

        return StatusItemModel(
            rows: rows,
            dimmed: expired,
            showStaleDot: state.status == .stale,
            notice: rows.isEmpty ? noRowsNotice : nil,
            hint: expired && !rows.isEmpty ? runClaudeHint : nil)
    }
}
