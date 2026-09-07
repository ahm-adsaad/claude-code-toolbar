import XCTest
@testable import ClaudeToolbarCore

final class ThresholdCueTrackerTests: XCTestCase {
    private let reset = Date(timeIntervalSince1970: 1_788_789_600)

    private func row(_ key: String, _ level: BarLevel, _ resetsAt: Date? = nil) -> TrackedRow {
        TrackedRow(key: key, level: level, resetsAt: resetsAt ?? reset)
    }

    func testFirstObservationOnlyPrimes() {
        XCTAssertNil(ThresholdCueTracker().observe([row("5h", .crit)]))
    }

    func testNoChangeGivesNoCue() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .warn)])
        XCTAssertNil(t.observe([row("5h", .warn)]))
    }

    func testRiseEmitsCue() {
        for (from, to, expected) in [(BarLevel.ok, BarLevel.warn, MascotCue.warn), (.ok, .crit, .crit), (.warn, .crit, .crit)] {
            let t = ThresholdCueTracker()
            _ = t.observe([row("5h", from)])
            XCTAssertEqual(t.observe([row("5h", to)]), expected, "\(from) → \(to)")
        }
    }

    func testStrongestCueWinsAcrossRows() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .ok), row("7d", .ok)])
        XCTAssertEqual(t.observe([row("5h", .warn), row("7d", .crit)]), .crit)
    }

    func testDropEmitsNothingAndReprimes() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .crit)])
        XCTAssertNil(t.observe([row("5h", .ok)]))
        XCTAssertEqual(t.observe([row("5h", .warn)]), .warn)
    }

    func testNewPeriodReprimesWithoutCue() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .ok)])
        XCTAssertNil(t.observe([row("5h", .crit, reset.addingTimeInterval(5 * 3600))]))
        XCTAssertNil(t.observe([row("5h", .crit, reset.addingTimeInterval(5 * 3600))]))
    }

    func testMissingResetDateStillCues() {
        let t = ThresholdCueTracker()
        _ = t.observe([TrackedRow(key: "7d Opus", level: .ok, resetsAt: nil)])
        XCTAssertEqual(t.observe([TrackedRow(key: "7d Opus", level: .warn, resetsAt: nil)]), .warn)
    }

    func testRowThatDisappearsIsReprimedWhenItReturns() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .ok), row("7d", .ok)])
        _ = t.observe([row("5h", .ok)])
        XCTAssertNil(t.observe([row("5h", .ok), row("7d", .crit)]))
    }

    func testSameLevelDoesNotRepeatTheCue() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .ok)])
        _ = t.observe([row("5h", .crit)])
        XCTAssertNil(t.observe([row("5h", .crit)]))
    }

    func testResetClearsHistory() {
        let t = ThresholdCueTracker()
        _ = t.observe([row("5h", .ok)])
        t.reset()
        XCTAssertNil(t.observe([row("5h", .crit)]))
    }

    func testTrackedRowsMapLabelsToTheirWindows() {
        let snapshot = UsageSnapshot(
            fiveHour: UsageWindow(utilization: 42, resetsAt: reset),
            sevenDay: UsageWindow(utilization: 18, resetsAt: reset.addingTimeInterval(3 * 86_400)),
            sevenDayOpus: UsageWindow(utilization: 75, resetsAt: nil),
            sevenDaySonnet: nil,
            fetchedAt: reset.addingTimeInterval(-3600))
        let model = StatusItemModel(rows: [
            StatusRow(label: "5h", utilization: 42, percentText: "42%", timeText: "2h", level: .ok, hasData: true),
            StatusRow(label: "7d", utilization: 18, percentText: "18%", timeText: "3d", level: .ok, hasData: true),
            StatusRow(label: "7d Opus", utilization: 75, percentText: "75%", timeText: "", level: .warn, hasData: true),
            StatusRow(label: "7d Sonnet", utilization: 0, percentText: "—", timeText: "", level: .ok, hasData: false),
        ], dimmed: false, showStaleDot: false, notice: nil, hint: nil)

        let rows = TrackedRows.from(model, snapshot: snapshot)
        XCTAssertEqual(rows, [
            TrackedRow(key: "5h", level: .ok, resetsAt: reset),
            TrackedRow(key: "7d", level: .ok, resetsAt: reset.addingTimeInterval(3 * 86_400)),
            TrackedRow(key: "7d Opus", level: .warn, resetsAt: nil),
            TrackedRow(key: "7d Sonnet", level: .ok, resetsAt: nil),
        ])
        XCTAssertTrue(TrackedRows.from(StatusItemModel(rows: [], dimmed: false, showStaleDot: false, notice: "x", hint: nil), snapshot: snapshot).isEmpty)
    }
}
