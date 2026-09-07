import Foundation

public enum PercentFormatter {
    public static func format(_ utilization: Double) -> String {
        let clamped = min(max(utilization, 0), 100)
        let rounded = Int(clamped.rounded(.toNearestOrAwayFromZero))
        return "\(rounded)%"
    }
}
