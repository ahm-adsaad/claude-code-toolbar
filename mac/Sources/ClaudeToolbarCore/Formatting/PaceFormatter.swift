import Foundation

/// Pace estimates are guesses, so they are rounded coarser the further out they reach.
public enum PaceFormatter {
    public static func format(_ t: TimeInterval, approximate: Bool = true) -> String {
        let m = t / 60
        let rounded =
            m < 10 ? max(1, nearest(m, 1)) :
            m < 57.5 ? nearest(m, 5) :
            m < 600 ? nearest(m, 30) :
            nearest(m, 60)
        return (approximate ? "~" : "") + compact(rounded)
    }

    /// How long the rate was measured over, in words.
    public static func span(_ t: TimeInterval) -> String {
        let m = t / 60
        if m < 57.5 { return plural(max(1, nearest(m, 5)), "minute") }
        let hours = nearest(m, 60) / 60
        return hours < 48 ? plural(hours, "hour") : plural(nearest(m, 1440) / 1440, "day")
    }

    private static func nearest(_ minutes: Double, _ step: Int) -> Int {
        Int((minutes / Double(step)).rounded(.toNearestOrAwayFromZero)) * step
    }

    private static func compact(_ minutes: Int) -> String {
        if minutes < 60 { return "\(minutes)m" }
        if minutes < 1440 { return minutes % 60 == 0 ? "\(minutes / 60)h" : "\(minutes / 60)h \(minutes % 60)m" }
        let hours = minutes % 1440 / 60
        return hours == 0 ? "\(minutes / 1440)d" : "\(minutes / 1440)d \(hours)h"
    }

    private static func plural(_ n: Int, _ unit: String) -> String { n == 1 ? "1 \(unit)" : "\(n) \(unit)s" }
}
