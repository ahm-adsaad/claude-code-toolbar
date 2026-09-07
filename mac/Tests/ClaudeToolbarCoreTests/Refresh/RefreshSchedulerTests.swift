import XCTest
@testable import ClaudeToolbarCore

final class RefreshSchedulerTests: XCTestCase {
    private let clock = FakeClock()

    func testDueImmediatelyAtStart() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        XCTAssertTrue(s.isDue(at: clock.now))
        XCTAssertFalse(s.isPaused)
    }

    func testSuccessSchedulesNextInterval() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        s.onSuccess(nextReset: nil)
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(60))
        XCTAssertFalse(s.isDue(at: clock.now.addingTimeInterval(59)))
        XCTAssertTrue(s.isDue(at: clock.now.addingTimeInterval(60)))
        XCTAssertEqual(s.currentBackoff, 0)
    }

    func testIntervalChangeAppliesToNextSuccess() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        s.intervalSeconds = 120
        s.onSuccess(nextReset: nil)
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(120))
    }

    func testResetSoonerThanIntervalShortensWait() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        s.onSuccess(nextReset: clock.now.addingTimeInterval(20))
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(21), "one second of grace after the reset")
    }

    func testResetInPastOrLaterThanIntervalIsIgnored() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        s.onSuccess(nextReset: clock.now.addingTimeInterval(-5))
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(60))
        s.onSuccess(nextReset: clock.now.addingTimeInterval(600))
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(60))
    }

    func testBackoffDoublesAndCaps() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        let expected: [TimeInterval] = [15, 30, 60, 120, 240, 300, 300]
        for delay in expected {
            s.onFailure(retryAfter: nil)
            XCTAssertEqual(s.currentBackoff, delay)
            XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(delay))
        }
        s.onSuccess(nextReset: nil)
        XCTAssertEqual(s.currentBackoff, 0)
    }

    func testRetryAfterLargerThanBackoffWins() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        s.onFailure(retryAfter: 100)
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(100))
        XCTAssertEqual(s.currentBackoff, 15)
        s.onFailure(retryAfter: 5)
        XCTAssertEqual(s.nextDue, clock.now.addingTimeInterval(30))
    }

    func testPauseAndImmediate() {
        let s = RefreshScheduler(clock: clock, intervalSeconds: 60)
        s.pause()
        XCTAssertTrue(s.isPaused)
        XCTAssertNil(s.nextDue)
        XCTAssertFalse(s.isDue(at: clock.now.addingTimeInterval(9999)))
        s.requestImmediate()
        XCTAssertFalse(s.isPaused)
        XCTAssertTrue(s.isDue(at: clock.now))
    }
}
