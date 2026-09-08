import XCTest
@testable import ClaudeToolbarCore

final class SessionTrackerTests: XCTestCase {
    private let t0 = Date(timeIntervalSince1970: 1_788_782_400)

    private func ev(_ kind: SessionEventKind, id: String = "s1", cwd: String? = "/work/my-repo", message: String? = nil, detail: String? = nil) -> SessionEvent {
        SessionEvent(kind: kind, sessionId: id, cwd: cwd, message: message, detail: detail)
    }

    func testStartCreatesIdleSessionNamedAfterTheFolder() {
        let t = SessionTracker()
        XCTAssertNil(t.apply(ev(.start), now: t0))
        XCTAssertEqual(t.sessions.count, 1)
        XCTAssertEqual(t.sessions[0].id, "s1")
        XCTAssertEqual(t.sessions[0].name, "my-repo")
        XCTAssertEqual(t.sessions[0].state, .idle)
        XCTAssertEqual(t.badge, MascotBadge.none)
        XCTAssertEqual(t.summary, "1 session · idle (my-repo)")
    }

    func testNameFallsBackToTheSessionIdPrefix() {
        let t = SessionTracker()
        _ = t.apply(ev(.start, id: "0123456789abcdef", cwd: nil), now: t0)
        XCTAssertEqual(t.sessions[0].name, "01234567")
        _ = t.apply(ev(.start, id: "0123456789abcdef", cwd: "C:\\Users\\u\\proj\\"), now: t0)
        XCTAssertEqual(t.sessions[0].name, "proj")
    }

    func testPromptThenStopCuesFinishedOnce() {
        let t = SessionTracker()
        XCTAssertNil(t.apply(ev(.promptSubmitted), now: t0))
        XCTAssertEqual(t.sessions[0].state, .working)
        XCTAssertEqual(t.badge, .working)
        XCTAssertEqual(t.apply(ev(.stopped), now: t0.addingTimeInterval(60)), .finished)
        XCTAssertEqual(t.badge, .finished)
        XCTAssertNil(t.apply(ev(.stopped), now: t0.addingTimeInterval(120)))
        XCTAssertEqual(t.summary, "1 session · finished (my-repo)")
    }

    func testAttentionCuesOnceUntilTheStateChanges() {
        let t = SessionTracker()
        XCTAssertEqual(t.apply(ev(.needsAttention, message: "Permission needed"), now: t0), .attention)
        XCTAssertEqual(t.sessions[0].message, "Permission needed")
        XCTAssertNil(t.apply(ev(.needsAttention, message: "Still waiting"), now: t0.addingTimeInterval(30)))
        XCTAssertEqual(t.badge, .attention)
        _ = t.apply(ev(.promptSubmitted), now: t0.addingTimeInterval(60))
        XCTAssertEqual(t.apply(ev(.needsAttention), now: t0.addingTimeInterval(120)), .attention)
    }

    func testFailedAlwaysCuesAndCarriesTheErrorType() {
        let t = SessionTracker()
        XCTAssertEqual(t.apply(ev(.failed, detail: "rate_limit"), now: t0), .failed)
        XCTAssertEqual(t.apply(ev(.failed, detail: "rate_limit"), now: t0), .failed)
        XCTAssertEqual(t.badge, .failed)
        XCTAssertEqual(t.sessions[0].message, "rate_limit")
    }

    func testInfoNeverChangesStateButRegistersNewSessions() {
        let t = SessionTracker()
        _ = t.apply(ev(.promptSubmitted), now: t0)
        XCTAssertNil(t.apply(ev(.info), now: t0))
        XCTAssertEqual(t.sessions[0].state, .working)
        XCTAssertNil(t.apply(ev(.info, id: "s2"), now: t0))
        XCTAssertEqual(t.sessions.count, 2)
    }

    func testEndedRemovesTheSession() {
        let t = SessionTracker()
        _ = t.apply(ev(.promptSubmitted), now: t0)
        XCTAssertNil(t.apply(ev(.ended), now: t0))
        XCTAssertTrue(t.sessions.isEmpty)
        XCTAssertNil(t.summary)
    }

    func testBadgePrecedenceAndSummaryAcrossSessions() {
        let t = SessionTracker()
        _ = t.apply(ev(.promptSubmitted, id: "a", cwd: "/x/web"), now: t0)
        _ = t.apply(ev(.stopped, id: "b", cwd: "/x/api"), now: t0.addingTimeInterval(1))
        _ = t.apply(ev(.needsAttention, id: "c", cwd: "/x/cli"), now: t0.addingTimeInterval(2))
        XCTAssertEqual(t.badge, .attention)
        XCTAssertEqual(t.summary, "3 sessions · 1 needs you (cli) · 1 finished (api) · 1 working (web)")
        XCTAssertEqual(t.sessions.map(\.id), ["c", "b", "a"])
    }

    func testAcknowledgeClearsFinishedAndFailedAndAssumesAttentionWasHandled() {
        let t = SessionTracker()
        _ = t.apply(ev(.stopped, id: "a"), now: t0)
        _ = t.apply(ev(.failed, id: "b"), now: t0)
        _ = t.apply(ev(.needsAttention, id: "c", message: "m"), now: t0)
        t.acknowledge()
        let states = Dictionary(uniqueKeysWithValues: t.sessions.map { ($0.id, $0.state) })
        XCTAssertEqual(states["a"], .idle)
        XCTAssertEqual(states["b"], .idle)
        XCTAssertEqual(states["c"], .working)
        XCTAssertTrue(t.sessions.allSatisfy { $0.message == nil })
        XCTAssertEqual(t.badge, .working)
    }

    func testPruneDropsStaleSessions() {
        let t = SessionTracker()
        _ = t.apply(ev(.promptSubmitted, id: "old"), now: t0)
        _ = t.apply(ev(.promptSubmitted, id: "new"), now: t0.addingTimeInterval(11 * 3600))
        t.prune(now: t0.addingTimeInterval(12 * 3600 + 60))
        XCTAssertEqual(t.sessions.map(\.id), ["new"])
        XCTAssertEqual(SessionTracker.staleAfter, 12 * 3600)
    }

    func testApplyKeepsTheNewestHostAndIgnoresNil() {
        let t = SessionTracker()
        let wt = SessionHost(pid: 70, name: "Windows Terminal", resolvedAt: t0)
        _ = t.apply(ev(.start), now: t0, host: wt)
        _ = t.apply(ev(.promptSubmitted), now: t0.addingTimeInterval(60))
        XCTAssertEqual(t.sessions[0].host, wt)
        let code = SessionHost(pid: 80, name: "VS Code", resolvedAt: t0.addingTimeInterval(120))
        _ = t.apply(ev(.stopped), now: t0.addingTimeInterval(120), host: code)
        XCTAssertEqual(t.session(id: "s1")?.host, code)
        t.acknowledge()
        XCTAssertEqual(t.session(id: "s1")?.host, code)
        XCTAssertNil(t.session(id: "missing"))
    }

    func testJumpCandidatePrefersTheMostUrgentThenTheNewest() {
        let t = SessionTracker()
        let host = SessionHost(pid: 70, name: "Terminal", resolvedAt: t0)
        _ = t.apply(ev(.stopped, id: "old-finished", cwd: "/a"), now: t0, host: host)
        _ = t.apply(ev(.needsAttention, id: "waiting", cwd: "/b", message: "permission"), now: t0.addingTimeInterval(-300), host: host)
        _ = t.apply(ev(.promptSubmitted, id: "busy", cwd: "/c"), now: t0.addingTimeInterval(60), host: host)
        XCTAssertEqual(t.jumpCandidate?.id, "waiting")
        t.acknowledge()   // waiting → working (older), old-finished → idle
        XCTAssertEqual(t.jumpCandidate?.id, "busy")
        _ = t.apply(ev(.promptSubmitted, id: "waiting", cwd: "/b"), now: t0.addingTimeInterval(120), host: host)
        XCTAssertEqual(t.jumpCandidate?.id, "waiting")
    }

    func testJumpCandidateSkipsSessionsWithoutAHost() {
        let t = SessionTracker()
        XCTAssertNil(t.jumpCandidate)
        _ = t.apply(ev(.needsAttention, id: "nohost", message: "x"), now: t0)
        XCTAssertNil(t.jumpCandidate)
        _ = t.apply(ev(.promptSubmitted, id: "hosted", cwd: "/h"), now: t0.addingTimeInterval(-60), host: SessionHost(pid: 1, name: "Terminal", resolvedAt: t0))
        XCTAssertEqual(t.jumpCandidate?.id, "hosted")
    }

    func testSessionLineShowsNameStateHostAndAge() {
        let host = SessionHost(pid: 80, name: "VS Code", resolvedAt: t0)
        let s = SessionInfo(id: "s1", name: "api", state: .needsAttention, updatedAt: t0, message: "permission", host: host)
        XCTAssertEqual(SessionLine.text(s, now: t0.addingTimeInterval(120)), "api · needs you · VS Code · 2 min")
        XCTAssertEqual(SessionLine.text(s, now: t0.addingTimeInterval(30)), "api · needs you · VS Code · now")
        XCTAssertEqual(SessionLine.text(s, now: t0.addingTimeInterval(3 * 3600 + 1200)), "api · needs you · VS Code · 3 h")
        let hostless = SessionInfo(id: "s1", name: "api", state: .working, updatedAt: t0, message: nil, host: nil)
        XCTAssertEqual(SessionLine.text(hostless, now: t0), "api · working · now")
        XCTAssertEqual(SessionLine.stateWord(.idle), "idle")
        XCTAssertEqual(SessionLine.stateWord(.failed), "failed")
        XCTAssertEqual(SessionLine.stateWord(.finished), "finished")
    }
}
