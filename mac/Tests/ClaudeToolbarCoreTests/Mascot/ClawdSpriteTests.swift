import XCTest
@testable import ClaudeToolbarCore

final class ClawdSpriteTests: XCTestCase {
    func testPoseFollowsTheArmAngle() {
        let cases: [(Double, ClawdPose)] = [(-60, .rest), (-30, .rest), (-29.9, .mid), (10, .mid), (10.1, .up), (40, .up), (40.1, .high), (75, .high)]
        for (angle, expected) in cases {
            XCTAssertEqual(ClawdSprite.poseFor(armAngle: angle), expected, "angle \(angle)")
        }
    }

    func testRestSpriteHasTheDecodedShape() {
        let cells = ClawdSprite.cells(for: .rest)
        XCTAssertEqual(cells.count, 56)
        XCTAssertEqual(Set(cells).count, 56)
        XCTAssertTrue(cells.allSatisfy { (0..<ClawdSprite.columns).contains($0.col) && (0..<ClawdSprite.rows).contains($0.row) })
        XCTAssertEqual(cells.filter { $0.kind == .eye }.sorted { $0.col < $1.col }, [SpriteCell(col: 5, row: 2, kind: .eye), SpriteCell(col: 12, row: 2, kind: .eye)])
        XCTAssertTrue(cells.contains(SpriteCell(col: 1, row: 3, kind: .body)))
        XCTAssertTrue(cells.contains(SpriteCell(col: 2, row: 3, kind: .body)))
        XCTAssertEqual(cells.filter { $0.row == 5 }.map(\.col).sorted(), [4, 6, 11, 13])
        XCTAssertFalse(cells.contains { $0.row == 0 })
        XCTAssertEqual(cells.filter { $0.row == 1 }.count, 12)
    }

    func testRightArmMovesWithThePose() {
        let expected: [ClawdPose: [SpriteCell]] = [
            .rest: [SpriteCell(col: 15, row: 3, kind: .body), SpriteCell(col: 16, row: 3, kind: .body)],
            .mid: [SpriteCell(col: 15, row: 3, kind: .body), SpriteCell(col: 16, row: 2, kind: .body)],
            .up: [SpriteCell(col: 15, row: 2, kind: .body), SpriteCell(col: 16, row: 1, kind: .body)],
            .high: [SpriteCell(col: 15, row: 1, kind: .body), SpriteCell(col: 16, row: 0, kind: .body)],
        ]
        for (pose, arm) in expected {
            let cells = ClawdSprite.cells(for: pose)
            XCTAssertEqual(cells.filter { $0.col >= 15 }.sorted { $0.col < $1.col }, arm, "\(pose)")
            XCTAssertEqual(cells.count, 56)
        }
    }

    func testColoursAndBadgeGeometryAreFixed() {
        XCTAssertEqual(ClawdSprite.bodyColor, "#FFD97757")
        XCTAssertEqual(ClawdSprite.eyeColor, "#FF1B1B1B")
        XCTAssertEqual(ClawdSprite.badgeCenterCol, 16.5)
        XCTAssertEqual(ClawdSprite.badgeCenterRow, 0.6)
        XCTAssertEqual(ClawdSprite.badgeRadiusCells, 2)
        XCTAssertNil(MascotBadge.none.hex)
        XCTAssertEqual(MascotBadge.attention.hex, "#FFD29922")
        XCTAssertEqual(MascotBadge.working.hex, "#FF3B82F6")
        XCTAssertEqual(MascotBadge.finished.hex, "#FF3FB950")
        XCTAssertEqual(MascotBadge.failed.hex, "#FFF85149")
        XCTAssertEqual([MascotBadge.working, .attention, .failed, .finished].max(), .attention)
    }
}
