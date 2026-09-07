import XCTest
@testable import ClaudeToolbarCore

final class AgoFormatterTests: XCTestCase {
    func testSeconds() {
        XCTAssertEqual(AgoFormatter.format(ago: 0), "0s")
        XCTAssertEqual(AgoFormatter.format(ago: 12.7), "12s")
        XCTAssertEqual(AgoFormatter.format(ago: -4), "0s")
    }

    func testMinutes() {
        XCTAssertEqual(AgoFormatter.format(ago: 60), "1m")
        XCTAssertEqual(AgoFormatter.format(ago: 3 * 60 + 40), "3m")
    }

    func testHoursAndBeyondUseRemainingFormat() {
        XCTAssertEqual(AgoFormatter.format(ago: 3600), "1h 0m")
        XCTAssertEqual(AgoFormatter.format(ago: 2 * 86400 + 5 * 3600), "2d 5h")
    }
}
