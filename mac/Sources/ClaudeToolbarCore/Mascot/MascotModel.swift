public struct MascotModel: Equatable, Sendable {
    public let visible: Bool
    public let level: BarLevel
    public let armAngle: Double
    public let dimmed: Bool

    public init(visible: Bool, level: BarLevel, armAngle: Double, dimmed: Bool) {
        self.visible = visible
        self.level = level
        self.armAngle = armAngle
        self.dimmed = dimmed
    }

    public static let hidden = MascotModel(visible: false, level: .ok, armAngle: WaveAnimation.restAngle, dimmed: false)
}

public enum MascotModelBuilder {
    public static func build(status: StatusItemModel, mascotMode: String, armAngle: Double) -> MascotModel {
        if MascotMode.normalize(mascotMode) == MascotMode.off || status.rows.isEmpty { return .hidden }
        let level = status.rows.map(\.level).max(by: { $0.rank < $1.rank }) ?? .ok
        return MascotModel(visible: true, level: level, armAngle: armAngle, dimmed: status.dimmed)
    }
}
