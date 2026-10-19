import XCTest
@testable import ClaudeToolbarCore

final class PaceFormatterTests: XCTestCase {
    func testFormatRoundsCoarserTheLongerItIs() {
        let cases: [(Double, String)] = [
            (0.4, "~1m"), (3, "~3m"), (47, "~45m"), (58, "~1h"), (95, "~1h 30m"),
            (372, "~6h"), (852, "~14h"), (1430, "~1d"), (1560, "~1d 2h"),
        ]
        for (minutes, expected) in cases {
            XCTAssertEqual(PaceFormatter.format(minutes * 60), expected, "\(minutes) minutes")
        }
    }

    func testFormatWithoutTilde() {
        XCTAssertEqual(PaceFormatter.format(9 * 3600, approximate: false), "9h")
    }

    func testSpanInWords() {
        let cases: [(Double, String)] = [
            (0.5, "1 minute"), (3, "5 minutes"), (30, "30 minutes"), (60, "1 hour"),
            (550, "9 hours"), (47 * 60, "47 hours"), (72 * 60, "3 days"),
        ]
        for (minutes, expected) in cases {
            XCTAssertEqual(PaceFormatter.span(minutes * 60), expected, "\(minutes) minutes")
        }
    }
}
