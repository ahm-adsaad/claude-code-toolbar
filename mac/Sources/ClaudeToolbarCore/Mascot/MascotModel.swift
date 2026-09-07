/// Session badge shown on Clawd's shoulder, ordered by strength.
public enum MascotBadge: Int, Comparable, Sendable {
    case none, working, finished, failed, attention

    public static func < (lhs: MascotBadge, rhs: MascotBadge) -> Bool { lhs.rawValue < rhs.rawValue }

    public var hex: String? {
        switch self {
        case .none: return nil
        case .working: return "#FF3B82F6"
        case .finished: return "#FF3FB950"
        case .failed: return "#FFF85149"
        case .attention: return "#FFD29922"
        }
    }
}

public struct MascotModel: Equatable, Sendable {
    public let visible: Bool
    public let armAngle: Double
    public let dimmed: Bool
    public let badge: MascotBadge
    public let badgeLit: Bool

    public init(visible: Bool, armAngle: Double, dimmed: Bool, badge: MascotBadge, badgeLit: Bool) {
        self.visible = visible
        self.armAngle = armAngle
        self.dimmed = dimmed
        self.badge = badge
        self.badgeLit = badgeLit
    }

    public static let hidden = MascotModel(visible: false, armAngle: WaveAnimation.restAngle, dimmed: false, badge: .none, badgeLit: false)
}

public enum MascotModelBuilder {
    public static func build(status: StatusItemModel, mascotMode: String, armAngle: Double, badge: MascotBadge = .none, badgeLit: Bool = true) -> MascotModel {
        if MascotMode.normalize(mascotMode) == MascotMode.off || status.rows.isEmpty { return .hidden }
        return MascotModel(visible: true, armAngle: armAngle, dimmed: status.dimmed, badge: badge, badgeLit: badgeLit)
    }
}
