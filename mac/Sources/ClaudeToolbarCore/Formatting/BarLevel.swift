public enum BarLevel: Sendable, Equatable {
    case ok
    case warn
    case crit
}

public enum BarLevelResolver {
    public static func resolve(utilization: Double, warnThreshold: Int, critThreshold: Int) -> BarLevel {
        if utilization < Double(warnThreshold) { return .ok }
        if utilization < Double(critThreshold) { return .warn }
        return .crit
    }
}
