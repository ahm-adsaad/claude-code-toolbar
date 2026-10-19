import Foundation

/// A line under a limit in the popover explaining its pace; `tint` colours it like a bar at that level.
public struct PaceLine: Equatable, Sendable {
    public let text: String
    public let tint: BarLevel?

    public init(text: String, tint: BarLevel? = nil) {
        self.text = text
        self.tint = tint
    }
}

public struct PopoverRow: Equatable, Sendable {
    public let name: String
    public let utilization: Double
    public let percentText: String
    public let level: BarLevel
    public let resetText: String?
    public let resetClockText: String?
    public let paceLines: [PaceLine]

    public init(name: String, utilization: Double, percentText: String, level: BarLevel, resetText: String?, resetClockText: String?, paceLines: [PaceLine] = []) {
        self.name = name
        self.utilization = utilization
        self.percentText = percentText
        self.level = level
        self.resetText = resetText
        self.resetClockText = resetClockText
        self.paceLines = paceLines
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
    public static func build(state: MonitorState, settings: AppSettings, now: Date, formatClock: (Date) -> String,
                             pace: [UsageLimit: PaceEstimate] = [:], hooksInstalled: Bool = true) -> PopoverModel {
        var rows: [PopoverRow] = []
        let a = settings.appearance
        // As in the status item, an expired login leaves numbers too old to project from.
        let estimates = state.status == .expired ? [:] : pace

        func add(_ name: String, _ limit: UsageLimit) {
            guard let window = state.snapshot?.window(limit) else { return }
            rows.append(PopoverRow(
                name: name,
                utilization: window.utilization,
                percentText: PercentFormatter.format(window.utilization),
                level: BarLevelResolver.resolve(utilization: window.utilization, warnThreshold: a.warnThreshold, critThreshold: a.critThreshold),
                resetText: window.resetsAt.map { "resets in " + RemainingTimeFormatter.format(resetsAt: $0, now: now) },
                resetClockText: window.resetsAt.map { "at " + formatClock($0) },
                paceLines: estimates[limit].map { paceLines($0, hooksInstalled: hooksInstalled) } ?? []))
        }

        add("Session", .fiveHour)
        add("Weekly", .sevenDay)
        add("Weekly Opus", .sevenDayOpus)
        add("Weekly Sonnet", .sevenDaySonnet)
        add("Weekly Fable", .sevenDayFable)

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

    private static func paceLines(_ e: PaceEstimate, hooksInstalled: Bool) -> [PaceLine] {
        var lines: [PaceLine] = []
        switch e.verdict {
        case .learning:
            return [PaceLine(text: "Learning your pace")]
        case .usedUp:
            return []
        case .runsOutFirst, .lasts:
            if e.verdict == .runsOutFirst, let left = e.workLeft {
                let tint: BarLevel = left < StatusItemModelBuilder.critWorkLeft ? .crit : .warn
                lines.append(PaceLine(text: "At your pace: about \(PaceFormatter.format(left, approximate: false)) of work left, runs out before the reset", tint: tint))
            } else {
                lines.append(PaceLine(text: "At your pace this lasts until the reset"))
            }
        }
        if let now = e.workLeftNow {
            lines.append(PaceLine(text: "With \(e.sessionsNow) sessions running: about \(PaceFormatter.format(now, approximate: false))"))
        }
        lines.append(PaceLine(text: e.basis == .work
            ? "Based on your last \(PaceFormatter.span(e.span)) of work"
            : "Based on clock time over the last \(PaceFormatter.span(e.span))" + (hooksInstalled ? "" : "; install the hooks for a better estimate")))
        return lines
    }
}
