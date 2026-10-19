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

    private func fableState(_ status: UsageStatus = .ok) -> MonitorState {
        let snap = UsageSnapshot(fiveHour: UsageWindow(utilization: 42, resetsAt: now.addingTimeInterval(2 * 3600)), sevenDay: nil, sevenDayOpus: nil, sevenDaySonnet: nil,
                                 sevenDayFable: UsageWindow(utilization: 72, resetsAt: now.addingTimeInterval(3 * 86_400)), fetchedAt: now)
        return MonitorState(status: status, snapshot: snap, lastSuccess: now, message: nil, credentials: creds)
    }

    private let runsOut = PaceEstimate(verdict: .runsOutFirst, workLeft: 6 * 3600, workLeftNow: nil, sessionsNow: 1, span: 9 * 3600, basis: .work)

    private func with(_ e: PaceEstimate, verdict: PaceVerdict? = nil, workLeftNow: TimeInterval? = nil, sessionsNow: Int? = nil, span: TimeInterval? = nil, basis: PaceBasis? = nil) -> PaceEstimate {
        PaceEstimate(verdict: verdict ?? e.verdict, workLeft: e.workLeft, workLeftNow: workLeftNow ?? e.workLeftNow, sessionsNow: sessionsNow ?? e.sessionsNow,
                     span: span ?? e.span, basis: basis ?? e.basis)
    }

    private func fableLines(_ e: PaceEstimate, status: UsageStatus = .ok, hooksInstalled: Bool = true) -> [PaceLine] {
        let m = PopoverModelBuilder.build(state: fableState(status), settings: .createDefault(), now: now, formatClock: clock,
                                          pace: [.sevenDayFable: e], hooksInstalled: hooksInstalled)
        XCTAssertEqual(m.rows.last?.name, "Weekly Fable")
        XCTAssertEqual(m.rows.first?.paceLines, [])
        return m.rows.last?.paceLines ?? []
    }

    func testPaceRunsOutFirstAddsAWarningAndItsBasis() {
        XCTAssertEqual(fableLines(runsOut), [
            PaceLine(text: "At your pace: about 6h of work left, runs out before the reset", tint: .warn),
            PaceLine(text: "Based on your last 9 hours of work", tint: nil),
        ])
    }

    func testPaceLasts() {
        XCTAssertEqual(fableLines(with(runsOut, verdict: .lasts)).map(\.text), ["At your pace this lasts until the reset", "Based on your last 9 hours of work"])
    }

    func testPaceLearningHasNoBasisLine() {
        XCTAssertEqual(fableLines(.learning), [PaceLine(text: "Learning your pace", tint: nil)])
    }

    func testPaceUsedUpAddsNothing() {
        XCTAssertEqual(fableLines(with(runsOut, verdict: .usedUp)), [])
    }

    func testPaceWithSeveralSessionsRunning() {
        XCTAssertEqual(fableLines(with(runsOut, workLeftNow: 40 * 60, sessionsNow: 3)).map(\.text), [
            "At your pace: about 6h of work left, runs out before the reset",
            "With 3 sessions running: about 40m",
            "Based on your last 9 hours of work",
        ])
    }

    func testPaceClockBasis() {
        let e = with(runsOut, span: 3600, basis: .clock)
        XCTAssertEqual(fableLines(e, hooksInstalled: false).last?.text, "Based on clock time over the last 1 hour; install the hooks for a better estimate")
        XCTAssertEqual(fableLines(e, hooksInstalled: true).last?.text, "Based on clock time over the last 1 hour")
    }

    func testPaceNothingWhenLoginExpired() {
        XCTAssertEqual(fableLines(runsOut, status: .expired), [])
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
