import XCTest
@testable import ClaudeToolbarCore

final class StatusItemModelBuilderTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_788_782_400)
    private let creds = CredentialsState.valid(source: "Keychain", accessToken: "t", expiresAt: Date(timeIntervalSince1970: 1_788_790_000), subscriptionType: nil)

    private var snapshot: UsageSnapshot {
        UsageSnapshot(fiveHour: UsageWindow(utilization: 42, resetsAt: now.addingTimeInterval(2 * 3600 + 13 * 60)),
                      sevenDay: UsageWindow(utilization: 91, resetsAt: now.addingTimeInterval(3 * 86_400 + 4 * 3600)),
                      sevenDayOpus: UsageWindow(utilization: 75, resetsAt: nil),
                      sevenDaySonnet: nil, fetchedAt: now)
    }

    private func state(_ status: UsageStatus, snapshot: UsageSnapshot? = nil) -> MonitorState {
        MonitorState(status: status, snapshot: snapshot, lastSuccess: snapshot?.fetchedAt, message: nil, credentials: creds)
    }

    func testOkBuildsDefaultRows() {
        let m = StatusItemModelBuilder.build(state: state(.ok, snapshot: snapshot), settings: .createDefault(), now: now)
        XCTAssertEqual(m.rows.count, 2)
        XCTAssertEqual(m.rows[0].label, "5h")
        XCTAssertEqual(m.rows[0].percentText, "42%")
        XCTAssertEqual(m.rows[0].timeText, "2h 13m")
        XCTAssertEqual(m.rows[0].level, .ok)
        XCTAssertTrue(m.rows[0].hasData)
        XCTAssertEqual(m.rows[1].label, "7d")
        XCTAssertEqual(m.rows[1].percentText, "91%")
        XCTAssertEqual(m.rows[1].timeText, "3d 4h")
        XCTAssertEqual(m.rows[1].level, .crit)
        XCTAssertFalse(m.dimmed)
        XCTAssertFalse(m.showStaleDot)
        XCTAssertNil(m.notice)
        XCTAssertNil(m.hint)
    }

    func testRowTogglesAndOrder() {
        var s = AppSettings.createDefault()
        s.rows.showFiveHour = false
        s.rows.showSevenDayOpus = true
        s.rows.showSevenDaySonnet = true
        let m = StatusItemModelBuilder.build(state: state(.ok, snapshot: snapshot), settings: s, now: now)
        XCTAssertEqual(m.rows.map(\.label), ["7d", "7d Opus", "7d Sonnet"])
        XCTAssertEqual(m.rows[1].level, .warn)
        XCTAssertEqual(m.rows[1].timeText, "", "no reset date means no time text")
        XCTAssertEqual(m.rows[2].percentText, "—")
        XCTAssertFalse(m.rows[2].hasData)
    }

    func testLoadingShowsPlaceholders() {
        let m = StatusItemModelBuilder.build(state: state(.loading), settings: .createDefault(), now: now)
        XCTAssertEqual(m.rows.count, 2)
        XCTAssertEqual(m.rows[0].percentText, "—")
        XCTAssertEqual(m.rows[0].utilization, 0)
        XCTAssertFalse(m.rows[0].hasData)
    }

    func testStaleShowsDot() {
        let m = StatusItemModelBuilder.build(state: state(.stale, snapshot: snapshot), settings: .createDefault(), now: now)
        XCTAssertTrue(m.showStaleDot)
        XCTAssertEqual(m.rows[0].percentText, "42%")
    }

    func testExpiredDimsAndHints() {
        let m = StatusItemModelBuilder.build(state: state(.expired, snapshot: snapshot), settings: .createDefault(), now: now)
        XCTAssertTrue(m.dimmed)
        XCTAssertEqual(m.hint, "↻ run claude")
        XCTAssertEqual(m.rows[0].percentText, "42%", "last numbers are kept")
    }

    func testNoCredentialsShowsNotice() {
        let m = StatusItemModelBuilder.build(state: state(.noCredentials), settings: .createDefault(), now: now)
        XCTAssertTrue(m.rows.isEmpty)
        XCTAssertEqual(m.notice, "Sign in with claude")
        XCTAssertFalse(m.dimmed)
        XCTAssertNil(m.hint)
    }

    func testNoRowsEnabled() {
        var s = AppSettings.createDefault()
        s.rows.showFiveHour = false
        s.rows.showSevenDay = false
        let m = StatusItemModelBuilder.build(state: state(.ok, snapshot: snapshot), settings: s, now: now)
        XCTAssertTrue(m.rows.isEmpty)
        XCTAssertEqual(m.notice, "No rows enabled")
    }

    func testCustomThresholds() {
        var s = AppSettings.createDefault()
        s.appearance.warnThreshold = 40
        s.appearance.critThreshold = 45
        let m = StatusItemModelBuilder.build(state: state(.ok, snapshot: snapshot), settings: s, now: now)
        XCTAssertEqual(m.rows[0].level, .warn)
    }
}
