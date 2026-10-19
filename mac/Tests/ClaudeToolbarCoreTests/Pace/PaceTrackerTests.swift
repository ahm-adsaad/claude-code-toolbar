import XCTest
@testable import ClaudeToolbarCore

final class PaceTrackerTests: XCTestCase {
    private let t0 = Date(timeIntervalSince1970: 1_792_400_400)
    private var reset: Date { t0.addingTimeInterval(5 * 3600) }

    private func five(_ utilization: Double, _ at: Date, resets: Date? = nil) -> UsageSnapshot {
        UsageSnapshot(fiveHour: UsageWindow(utilization: utilization, resetsAt: resets ?? reset), sevenDay: nil, sevenDayOpus: nil, sevenDaySonnet: nil, fetchedAt: at)
    }

    private func minutes(_ m: Double) -> Date { t0.addingTimeInterval(m * 60) }

    /// One 5-hour reading every 10 minutes from `from`, with `sessions` working throughout.
    private func run(_ t: PaceTracker, sessions: Int, from: Date, _ readings: [Double]) {
        for (i, u) in readings.enumerated() {
            t.record(five(u, from.addingTimeInterval(Double(i) * 600)), workingSessions: sessions)
        }
    }

    private func steady() -> PaceTracker {
        let t = PaceTracker()
        run(t, sessions: 1, from: t0, [10, 15, 20, 25, 30, 35, 40])
        return t
    }

    func testLearningBelowThreePoints() {
        let t = PaceTracker()
        run(t, sessions: 1, from: t0, [10, 11, 11, 12])
        XCTAssertEqual(t.estimate(.fiveHour, now: minutes(30), workingSessions: 1).verdict, .learning)
    }

    func testSteadyPaceRunsOutFirst() {
        let e = steady().estimate(.fiveHour, now: minutes(60), workingSessions: 1)
        XCTAssertEqual(e.verdict, .runsOutFirst)
        XCTAssertEqual(e.workLeft, 2 * 3600)
        XCTAssertEqual(e.basis, .work)
        XCTAssertEqual(e.span, 30 * 60)
    }

    func testSlowPaceLasts() {
        let t = PaceTracker()
        run(t, sessions: 1, from: t0, [10, 12, 14, 16, 18, 20, 22])
        let e = t.estimate(.fiveHour, now: minutes(60), workingSessions: 1)
        XCTAssertEqual(e.verdict, .lasts)
        XCTAssertEqual(e.workLeft, 6.5 * 3600)
    }

    func testIdleTimeDoesNotChangeWorkLeft() {
        let t = PaceTracker()
        run(t, sessions: 1, from: t0, [10, 15, 20, 25])
        t.advance(workingSessions: 0, now: minutes(30))
        run(t, sessions: 0, from: minutes(40), Array(repeating: 25, count: 12))
        t.advance(workingSessions: 1, now: minutes(150))
        run(t, sessions: 1, from: minutes(160), [30, 35, 40])
        XCTAssertEqual(t.estimate(.fiveHour, now: minutes(180), workingSessions: 1).workLeft, 2 * 3600)
    }

    func testParallelSessions() {
        let t = PaceTracker()
        run(t, sessions: 2, from: t0, [10, 20, 30, 40])
        let now = minutes(30)
        XCTAssertEqual(t.estimate(.fiveHour, now: now, workingSessions: 2).workLeft, 3600)
        let three = t.estimate(.fiveHour, now: now, workingSessions: 3)
        XCTAssertEqual(three.workLeftNow, 40 * 60)
        XCTAssertEqual(three.sessionsNow, 3)
        XCTAssertNil(t.estimate(.fiveHour, now: now, workingSessions: 1).workLeftNow)
    }

    func testResetStartsOver() {
        let t = steady()
        t.record(five(2, minutes(70), resets: reset.addingTimeInterval(5 * 3600)), workingSessions: 1)
        XCTAssertEqual(t.estimate(.fiveHour, now: minutes(70), workingSessions: 1).verdict, .learning)
    }

    func testSmallResetShiftKeepsHistory() {
        let t = steady()
        let now = minutes(60)
        let before = t.estimate(.fiveHour, now: now, workingSessions: 1)
        t.record(five(40, now, resets: reset.addingTimeInterval(30)), workingSessions: 1)
        XCTAssertEqual(t.estimate(.fiveHour, now: now, workingSessions: 1), before)
    }

    func testSmallDipKeepsHistory() {
        let t = steady()
        t.record(five(39, minutes(70)), workingSessions: 1)
        XCTAssertNotEqual(t.estimate(.fiveHour, now: minutes(70), workingSessions: 1).verdict, .learning)
    }

    func testClockFallbackWhenNoWorkRecorded() {
        let t = PaceTracker()
        run(t, sessions: 0, from: t0, [10, 15, 20, 25, 30, 35, 40])
        let e = t.estimate(.fiveHour, now: minutes(60), workingSessions: 0)
        XCTAssertEqual(e.verdict, .runsOutFirst)
        XCTAssertEqual(e.workLeft, 2 * 3600)
        XCTAssertEqual(e.basis, .clock)
        XCTAssertEqual(e.span, 3600)
    }

    func testUsedUpAtHundred() {
        let t = PaceTracker()
        t.record(five(100, t0), workingSessions: 1)
        XCTAssertEqual(t.estimate(.fiveHour, now: t0, workingSessions: 1).verdict, .usedUp)
    }

    func testSuspendSkipsTheGap() {
        let t = PaceTracker()
        t.advance(workingSessions: 1, now: t0)
        t.suspend(now: minutes(10))
        t.advance(workingSessions: 1, now: minutes(8 * 60))
        XCTAssertEqual(t.export().workMinutes, 10)
    }

    func testClockGoingBackwardsAddsNothing() {
        let t = PaceTracker()
        t.advance(workingSessions: 1, now: t0)
        t.advance(workingSessions: 1, now: minutes(10))
        t.advance(workingSessions: 1, now: minutes(5))
        XCTAssertEqual(t.export().workMinutes, 10)
    }

    func testLimitAppearingLaterIsIndependent() {
        let t = PaceTracker()
        run(t, sessions: 1, from: t0, [10, 15, 20])
        let withFable = UsageSnapshot(fiveHour: UsageWindow(utilization: 25, resetsAt: reset), sevenDay: nil, sevenDayOpus: nil, sevenDaySonnet: nil,
                                      sevenDayFable: UsageWindow(utilization: 30, resetsAt: t0.addingTimeInterval(3 * 86_400)), fetchedAt: minutes(30))
        t.record(withFable, workingSessions: 1)
        XCTAssertEqual(t.estimate(.sevenDayFable, now: minutes(30), workingSessions: 1).verdict, .learning)
        XCTAssertEqual(t.estimate(.sevenDayOpus, now: minutes(30), workingSessions: 1).verdict, .learning)
    }

    func testExportAndRestoreGiveTheSameEstimate() {
        let t = steady()
        let restored = PaceTracker(restored: t.export())
        let now = minutes(60)
        XCTAssertEqual(restored.estimate(.fiveHour, now: now, workingSessions: 1), t.estimate(.fiveHour, now: now, workingSessions: 1))
    }
}
