import Foundation

public enum AgoFormatter {
    public static func format(ago: TimeInterval) -> String {
        let elapsed = max(ago, 0)
        if elapsed < 60 { return "\(Int(elapsed))s" }
        if elapsed < 3600 { return "\(Int(elapsed / 60))m" }
        return RemainingTimeFormatter.format(remaining: elapsed)
    }
}
