import XCTest
@testable import ClaudeToolbarCore

final class PopoverModelBuilderTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_788_782_400)
    private let creds = CredentialsState.missing(source: "Keychain")
    private let clock: (Date) -> String = { _ in "4:13 PM" }

    private var snapshot: UsageSnapshot {
        UsageSnapshot(fiveHour: UsageWindow(utilization: 42.4, resetsAt: now.addingTimeInterval(2 * 3600 + 13 * 60)),
                      sevenDay: UsageWindow(utilization: 18, resetsAt: nil),
                      sevenDayOpus: nil,
                      sevenDaySonnet: UsageWindow(utilization: 95, resetsAt: now.addingTimeInterval(60)),
                      fetchedAt: now.addingTimeInterval(-12))
    }

    func testRowsUpdatedAndStatus() {
        let state = MonitorState(status: .ok, snapshot: snapshot, lastSuccess: now.addingTimeInterval(-12), message: nil, credentials: creds)
        let m = PopoverModelBuilder.build(state: state, settings: .createDefault(), now: now, formatClock: clock)
        XCTAssertEqual(m.rows.map(\.name), ["Session", "Weekly", "Weekly Sonnet"])
        XCTAssertEqual(m.rows[0].percentText, "42%")
        XCTAssertEqual(m.rows[0].utilization, 42.4)
        XCTAssertEqual(m.rows[0].level, .ok)
        XCTAssertEqual(m.rows[0].resetText, "resets in 2h 13m")
        XCTAssertEqual(m.rows[0].resetClockText, "at 4:13 PM")
        XCTAssertNil(m.rows[1].resetText)
        XCTAssertNil(m.rows[1].resetClockText)
        XCTAssertEqual(m.rows[2].level, .crit)
        XCTAssertEqual(m.updatedText, "Updated 12s ago")
        XCTAssertEqual(m.statusText, "OK")
    }

    func testStatusTexts() {
        func text(_ status: UsageStatus, message: String? = nil) -> String {
            PopoverModelBuilder.build(state: MonitorState(status: status, snapshot: nil, lastSuccess: nil, message: message, credentials: creds),
                                      settings: .createDefault(), now: now, formatClock: clock).statusText
        }
        XCTAssertEqual(text(.loading), "Loading…")
        XCTAssertEqual(text(.stale, message: "offline"), "Stale: offline")
        XCTAssertEqual(text(.stale), "Stale: no connection")
        XCTAssertEqual(text(.expired), "Login expired — run claude")
        XCTAssertEqual(text(.noCredentials), "Not signed in — run claude")
    }

    func testNoSnapshotMeansNoRowsOrUpdated() {
        let m = PopoverModelBuilder.build(state: .initial(credentials: creds), settings: .createDefault(), now: now, formatClock: clock)
        XCTAssertTrue(m.rows.isEmpty)
        XCTAssertNil(m.updatedText)
    }
}
