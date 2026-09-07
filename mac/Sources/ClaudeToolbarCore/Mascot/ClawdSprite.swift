public enum ClawdPose: Sendable { case rest, mid, up, high }

public enum CellKind: Sendable { case body, eye }

public struct SpriteCell: Hashable, Sendable {
    public let col: Int
    public let row: Int
    public let kind: CellKind

    public init(col: Int, row: Int, kind: CellKind) {
        self.col = col
        self.row = row
        self.kind = kind
    }
}

/// Clawd as data: an 18×6 grid of w×2w cells decoded from the Claude Code CLI's block sprite. Row 0 is only used by the raised arm.
public enum ClawdSprite {
    public static let columns = 18
    public static let rows = 6
    public static let bodyColor = "#FFD97757"
    public static let eyeColor = "#FF1B1B1B"
    public static let badgeCenterCol = 16.5
    public static let badgeCenterRow = 0.6
    public static let badgeRadiusCells = 2.0

    public static func poseFor(armAngle: Double) -> ClawdPose {
        if armAngle <= -30 { return .rest }
        if armAngle <= 10 { return .mid }
        if armAngle <= 40 { return .up }
        return .high
    }

    public static func cells(for pose: ClawdPose) -> [SpriteCell] {
        var cells: [SpriteCell] = []
        cells.reserveCapacity(56)
        for c in 3...14 { cells.append(SpriteCell(col: c, row: 1, kind: .body)) }
        for c in 3...14 { cells.append(SpriteCell(col: c, row: 2, kind: c == 5 || c == 12 ? .eye : .body)) }
        for c in 1...14 { cells.append(SpriteCell(col: c, row: 3, kind: .body)) }
        for c in 3...14 { cells.append(SpriteCell(col: c, row: 4, kind: .body)) }
        for c in [4, 6, 11, 13] { cells.append(SpriteCell(col: c, row: 5, kind: .body)) }

        let (inner, outer): (Int, Int)
        switch pose {
        case .rest: (inner, outer) = (3, 3)
        case .mid: (inner, outer) = (3, 2)
        case .up: (inner, outer) = (2, 1)
        case .high: (inner, outer) = (1, 0)
        }
        cells.append(SpriteCell(col: 15, row: inner, kind: .body))
        cells.append(SpriteCell(col: 16, row: outer, kind: .body))
        return cells
    }
}
