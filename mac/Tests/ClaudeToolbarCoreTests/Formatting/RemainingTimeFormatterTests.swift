import XCTest
@testable import ClaudeToolbarCore

final class RemainingTimeFormatterTests: XCTestCase {
    func testPastAndZeroAreNow() {
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 0), "now")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: -5), "now")
    }

    func testUnderAMinute() {
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 1), "<1m")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 59), "<1m")
    }

    func testMinutes() {
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 60), "1m")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 13 * 60 + 30), "13m")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 59 * 60 + 59), "59m")
    }

    func testHours() {
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 3600), "1h 0m")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 2 * 3600 + 13 * 60), "2h 13m")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 23 * 3600 + 59 * 60 + 59), "23h 59m")
    }

    func testDays() {
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 86400), "1d 0h")
        XCTAssertEqual(RemainingTimeFormatter.format(remaining: 3 * 86400 + 4 * 3600 + 20 * 60), "3d 4h")
    }

    func testFromDates() {
        let now = Date(timeIntervalSince1970: 1_788_782_400)
        let reset = now.addingTimeInterval(2 * 3600 + 13 * 60)
        XCTAssertEqual(RemainingTimeFormatter.format(resetsAt: reset, now: now), "2h 13m")
        XCTAssertEqual(RemainingTimeFormatter.format(resetsAt: now, now: reset), "now")
    }
}
