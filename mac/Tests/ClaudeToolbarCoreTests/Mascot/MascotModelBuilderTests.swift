import XCTest
@testable import ClaudeToolbarCore

final class MascotModelBuilderTests: XCTestCase {
    private func status(dimmed: Bool = false) -> StatusItemModel {
        StatusItemModel(rows: [StatusRow(label: "5h", utilization: 10, percentText: "10%", timeText: "", level: .ok, hasData: true)],
                        dimmed: dimmed, showStaleDot: false, notice: nil, hint: nil)
    }

    func testOffModeHidesTheMascot() {
        XCTAssertEqual(MascotModelBuilder.build(status: status(), mascotMode: "off", armAngle: 12, badge: .attention), .hidden)
    }

    func testNoticeWithoutRowsHidesTheMascot() {
        let notice = StatusItemModel(rows: [], dimmed: false, showStaleDot: false, notice: "Sign in with claude", hint: nil)
        XCTAssertEqual(MascotModelBuilder.build(status: notice, mascotMode: "full", armAngle: 12), .hidden)
    }

    func testArmAngleBadgeAndDimmingPassThrough() {
        let m = MascotModelBuilder.build(status: status(dimmed: true), mascotMode: "hover", armAngle: 33, badge: .finished, badgeLit: false)
        XCTAssertTrue(m.visible)
        XCTAssertEqual(m.armAngle, 33)
        XCTAssertTrue(m.dimmed)
        XCTAssertEqual(m.badge, .finished)
        XCTAssertFalse(m.badgeLit)
    }

    func testDefaultsAreNoBadgeAndLit() {
        let m = MascotModelBuilder.build(status: status(), mascotMode: "full", armAngle: WaveAnimation.restAngle)
        XCTAssertEqual(m.badge, MascotBadge.none)
        XCTAssertTrue(m.badgeLit)
    }
}
