import XCTest
@testable import ClaudeToolbarCore

final class UsageResponseParserTests: XCTestCase {
    private let fetchedAt = Date(timeIntervalSince1970: 1_788_782_400)

    private let fullPayload = """
    {
      "five_hour":        { "utilization": 42.0, "resets_at": "2026-09-07T14:13:00+00:00" },
      "seven_day":        { "utilization": 18.0, "resets_at": "2026-09-10T16:00:00+00:00" },
      "seven_day_opus":   { "utilization": 5.5,  "resets_at": "2026-09-10T16:00:00+00:00" },
      "seven_day_sonnet": null,
      "extra_usage":      { "is_enabled": false }
    }
    """

    private func ok(_ result: UsageResult, file: StaticString = #filePath, line: UInt = #line) -> UsageSnapshot? {
        if case .ok(let snapshot) = result { return snapshot }
        XCTFail("expected .ok, got \(result)", file: file, line: line)
        return nil
    }

    func testParsesFullPayload() {
        guard let s = ok(UsageResponseParser.parse(fullPayload, fetchedAt: fetchedAt)) else { return }
        XCTAssertEqual(s.fiveHour?.utilization, 42.0)
        XCTAssertEqual(s.fiveHour?.resetsAt, Date(timeIntervalSince1970: 1_788_790_380))
        XCTAssertEqual(s.sevenDay?.utilization, 18.0)
        XCTAssertEqual(s.sevenDayOpus?.utilization, 5.5)
        XCTAssertNil(s.sevenDaySonnet)
        XCTAssertEqual(s.fetchedAt, fetchedAt)
    }

    func testIntegerUtilizationIsAccepted() {
        guard let s = ok(UsageResponseParser.parse(#"{ "five_hour": { "utilization": 7 } }"#, fetchedAt: fetchedAt)) else { return }
        XCTAssertEqual(s.fiveHour?.utilization, 7)
        XCTAssertNil(s.fiveHour?.resetsAt)
    }

    func testClampsUtilization() {
        guard let s = ok(UsageResponseParser.parse(#"{ "five_hour": { "utilization": 140 }, "seven_day": { "utilization": -3 } }"#, fetchedAt: fetchedAt)) else { return }
        XCTAssertEqual(s.fiveHour?.utilization, 100)
        XCTAssertEqual(s.sevenDay?.utilization, 0)
    }

    func testMissingUtilizationDropsWindow() {
        guard let s = ok(UsageResponseParser.parse(#"{ "five_hour": { "resets_at": "2026-09-07T14:13:00+00:00" } }"#, fetchedAt: fetchedAt)) else { return }
        XCTAssertNil(s.fiveHour)
    }

    func testBadResetDateIsIgnoredButWindowKept() {
        guard let s = ok(UsageResponseParser.parse(#"{ "five_hour": { "utilization": 1, "resets_at": "soon" } }"#, fetchedAt: fetchedAt)) else { return }
        XCTAssertEqual(s.fiveHour?.utilization, 1)
        XCTAssertNil(s.fiveHour?.resetsAt)
    }

    func testFractionalSecondsAndZulu() {
        guard let s = ok(UsageResponseParser.parse(#"{ "five_hour": { "utilization": 1, "resets_at": "2026-09-07T14:13:00.250Z" }, "seven_day": { "utilization": 1, "resets_at": "2026-09-07T14:13:00Z" } }"#, fetchedAt: fetchedAt)) else { return }
        XCTAssertEqual(s.fiveHour?.resetsAt?.timeIntervalSince1970 ?? 0, 1_788_790_380.25, accuracy: 0.001)
        XCTAssertEqual(s.sevenDay?.resetsAt, Date(timeIntervalSince1970: 1_788_790_380))
    }

    func testEmptyObjectGivesEmptySnapshot() {
        guard let s = ok(UsageResponseParser.parse("{}", fetchedAt: fetchedAt)) else { return }
        XCTAssertTrue(s.windows.isEmpty)
        XCTAssertNil(s.nextReset)
    }

    func testEmptyAndInvalidInputsFail() {
        guard case .failed = UsageResponseParser.parse("", fetchedAt: fetchedAt) else { return XCTFail("empty should fail") }
        guard case .failed = UsageResponseParser.parse("   ", fetchedAt: fetchedAt) else { return XCTFail("blank should fail") }
        guard case .failed = UsageResponseParser.parse("{ not json", fetchedAt: fetchedAt) else { return XCTFail("bad json should fail") }
        guard case .failed(let message) = UsageResponseParser.parse("[1, 2]", fetchedAt: fetchedAt) else { return XCTFail("array should fail") }
        XCTAssertEqual(message, "Response is not a JSON object")
    }
}
