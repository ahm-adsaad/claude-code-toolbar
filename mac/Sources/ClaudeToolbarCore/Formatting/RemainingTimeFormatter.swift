import Foundation

public enum RemainingTimeFormatter {
    public static func format(remaining: TimeInterval) -> String {
        if remaining <= 0 { return "now" }
        if remaining < 60 { return "<1m" }
        let totalMinutes = Int(remaining / 60)
        if remaining < 3600 { return "\(totalMinutes)m" }
        let totalHours = Int(remaining / 3600)
        if remaining < 86_400 { return "\(totalHours)h \(totalMinutes % 60)m" }
        let totalDays = Int(remaining / 86_400)
        return "\(totalDays)d \(totalHours % 24)h"
    }

    public static func format(resetsAt: Date, now: Date) -> String {
        format(remaining: resetsAt.timeIntervalSince(now))
    }
}
