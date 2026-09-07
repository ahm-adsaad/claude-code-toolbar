import XCTest
@testable import ClaudeToolbarCore

final class MascotModelBuilderTests: XCTestCase {
    private func status(dimmed: Bool = false, _ levels: BarLevel...) -> StatusItemModel {
        StatusItemModel(rows: levels.enumerated().map { i, level in
            StatusRow(label: "r\(i)", utilization: 10, percentText: "10%", timeText: "", level: level, hasData: true)
        }, dimmed: dimmed, showStaleDot: false, notice: nil, hint: nil)
    }

    func testOffModeHidesTheMascot() {
        XCTAssertEqual(MascotModelBuilder.build(status: status(.ok), mascotMode: "off", armAngle: 12), .hidden)
    }

    func testNoticeWithoutRowsHidesTheMascot() {
        let notice = StatusItemModel(rows: [], dimmed: false, showStaleDot: false, notice: "Sign in with claude", hint: nil)
        XCTAssertEqual(MascotModelBuilder.build(status: notice, mascotMode: "full", armAngle: 12), .hidden)
    }

    func testWorstLevelWinsAndArmAnglePassesThrough() {
        let m = MascotModelBuilder.build(status: status(.ok, .crit, .warn), mascotMode: "full", armAngle: 33)
        XCTAssertTrue(m.visible)
        XCTAssertEqual(m.level, .crit)
        XCTAssertEqual(m.armAngle, 33)
        XCTAssertFalse(m.dimmed)
    }

    func testHoverModeIsVisibleAndDimmedFollowsTheWidget() {
        let m = MascotModelBuilder.build(status: status(dimmed: true, .ok), mascotMode: "hover", armAngle: WaveAnimation.restAngle)
        XCTAssertTrue(m.visible)
        XCTAssertEqual(m.level, .ok)
        XCTAssertTrue(m.dimmed)
    }

    func testUnknownModeBehavesLikeFull() {
        XCTAssertTrue(MascotModelBuilder.build(status: status(.ok), mascotMode: "sparkles", armAngle: 0).visible)
    }
}
