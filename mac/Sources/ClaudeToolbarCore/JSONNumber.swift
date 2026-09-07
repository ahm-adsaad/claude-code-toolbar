import Foundation

/// JSONSerialization yields NSNumber on Darwin and may yield Int/Double on corelibs builds; accept all of them.
/// Booleans arrive as 0/1, which is harmless for the fields this app reads.
enum JSONNumber {
    static func double(_ value: Any?) -> Double? {
        switch value {
        case let d as Double: return d
        case let i as Int: return Double(i)
        case let n as NSNumber: return n.doubleValue
        default: return nil
        }
    }
}
