import XCTest
@testable import ClaudeToolbarCore

final class PercentFormatterTests: XCTestCase {
    func testRoundsHalfAwayFromZero() {
        XCTAssertEqual(PercentFormatter.format(42.0), "42%")
        XCTAssertEqual(PercentFormatter.format(42.5), "43%")
        XCTAssertEqual(PercentFormatter.format(42.49), "42%")
        XCTAssertEqual(PercentFormatter.format(0.4), "0%")
    }

    func testClamps() {
        XCTAssertEqual(PercentFormatter.format(-3), "0%")
        XCTAssertEqual(PercentFormatter.format(140), "100%")
    }
}
