import XCTest
@testable import ClaudeToolbarCore

final class UsageMonitorTests: XCTestCase {
    private let clock = FakeClock()
    private var expires: Date { clock.now.addingTimeInterval(3600) }
    private var validCreds: CredentialsState { .valid(source: "Keychain", accessToken: "tok", expiresAt: expires, subscriptionType: "max") }

    private func snapshot(resetIn seconds: TimeInterval = 3600) -> UsageSnapshot {
        UsageSnapshot(fiveHour: UsageWindow(utilization: 42, resetsAt: clock.now.addingTimeInterval(seconds)),
                      sevenDay: UsageWindow(utilization: 18, resetsAt: clock.now.addingTimeInterval(86_400)),
                      sevenDayOpus: nil, sevenDaySonnet: nil, fetchedAt: clock.now)
    }

    private func makeMonitor(_ creds: CredentialsState, _ results: [UsageResult]) -> (UsageMonitor, FakeCredentialsSource, FakeUsageClient) {
        let source = FakeCredentialsSource(creds, sourceName: "Keychain")
        let client = FakeUsageClient(results)
        let monitor = UsageMonitor(credentials: source, client: client, clock: clock, intervalSeconds: 60)
        return (monitor, source, client)
    }

    func testInitialState() async {
        let (monitor, _, _) = makeMonitor(.missing(source: "Keychain"), [])
        let state = await monitor.state
        XCTAssertEqual(state.status, .loading)
        XCTAssertNil(state.snapshot)
        XCTAssertEqual(state.credentials, .missing(source: "Keychain"))
        let paused = await monitor.isPaused
        XCTAssertFalse(paused)
    }

    func testMissingCredentialsPausesWithoutFetching() async {
        let (monitor, _, client) = makeMonitor(.missing(source: "Keychain"), [])
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .noCredentials)
        XCTAssertEqual(state.message, "Credentials not found")
        XCTAssertEqual(client.callCount, 0)
        let paused = await monitor.isPaused
        XCTAssertTrue(paused)
    }

    func testInvalidCredentialsReportReason() async {
        let (monitor, _, _) = makeMonitor(.invalid(source: "Keychain", reason: "accessToken missing"), [])
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .noCredentials)
        XCTAssertEqual(state.message, "accessToken missing")
    }

    func testExpiredCredentials() async {
        let (monitor, _, client) = makeMonitor(.expired(source: "Keychain", expiresAt: clock.now, subscriptionType: nil), [])
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .expired)
        XCTAssertEqual(state.message, "Login expired")
        XCTAssertEqual(client.callCount, 0)
    }

    func testSuccessfulFetch() async {
        let snap = snapshot()
        let (monitor, _, client) = makeMonitor(validCreds, [.ok(snap)])
        let box = StateBox()
        await monitor.setStateHandler { box.append($0) }
        await monitor.refresh()
        let published = box.states
        let state = await monitor.state
        XCTAssertEqual(state.status, .ok)
        XCTAssertEqual(state.snapshot, snap)
        XCTAssertEqual(state.lastSuccess, snap.fetchedAt)
        XCTAssertNil(state.message)
        XCTAssertEqual(state.credentials, validCreds)
        XCTAssertEqual(client.lastToken, "tok")
        XCTAssertEqual(published.count, 1)
        let due = await monitor.nextDue
        XCTAssertEqual(due, clock.now.addingTimeInterval(60))
    }

    func testUnauthorizedMarksExpiredAndPauses() async {
        let (monitor, _, _) = makeMonitor(validCreds, [.unauthorized])
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .expired)
        XCTAssertEqual(state.message, "Token rejected")
        let paused = await monitor.isPaused
        XCTAssertTrue(paused)
    }

    func testFailureBeforeAnyDataIsLoading() async {
        let (monitor, _, _) = makeMonitor(validCreds, [.failed("boom")])
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .loading)
        XCTAssertEqual(state.message, "boom")
        let due = await monitor.nextDue
        XCTAssertEqual(due, clock.now.addingTimeInterval(15))
    }

    func testFailureAfterDataIsStaleAndKeepsSnapshot() async {
        let snap = snapshot()
        let (monitor, _, _) = makeMonitor(validCreds, [.ok(snap), .failed("offline")])
        await monitor.refresh()
        await monitor.requestRefresh()
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .stale)
        XCTAssertEqual(state.snapshot, snap)
        XCTAssertEqual(state.lastSuccess, snap.fetchedAt)
        XCTAssertEqual(state.message, "offline")
    }

    func testRateLimitedUsesRetryAfter() async {
        let (monitor, _, _) = makeMonitor(validCreds, [.rateLimited(retryAfter: 200)])
        await monitor.refresh()
        let state = await monitor.state
        XCTAssertEqual(state.status, .loading)
        XCTAssertEqual(state.message, "Rate limited")
        let due = await monitor.nextDue
        XCTAssertEqual(due, clock.now.addingTimeInterval(200))
    }

    func testTickOnlyFetchesWhenDue() async {
        let (monitor, _, client) = makeMonitor(validCreds, [.ok(snapshot())])
        await monitor.tick()
        XCTAssertEqual(client.callCount, 1)
        clock.advance(30)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 1)
        clock.advance(30)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 2)
    }

    func testResetPassingTriggersOneRefresh() async {
        let (monitor, _, client) = makeMonitor(validCreds, [.ok(snapshot(resetIn: 10))])
        await monitor.tick()
        XCTAssertEqual(client.callCount, 1)
        let due = await monitor.nextDue
        XCTAssertEqual(due, clock.now.addingTimeInterval(11), "scheduler shortened the wait to just after the reset")
        clock.advance(11)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 2)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 2, "same reset must not trigger again")
    }

    func testSetIntervalRequestsImmediateRefresh() async {
        let (monitor, _, client) = makeMonitor(validCreds, [.ok(snapshot())])
        await monitor.refresh()
        await monitor.setIntervalSeconds(120)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 2)
        let due = await monitor.nextDue
        XCTAssertEqual(due, clock.now.addingTimeInterval(120))
        await monitor.setIntervalSeconds(120)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 2, "unchanged interval is a no-op")
    }

    func testPausedMonitorRechecksCredentialsEvery30Seconds() async {
        let (monitor, source, client) = makeMonitor(.missing(source: "Keychain"), [.ok(snapshot())])
        await monitor.tick()
        XCTAssertEqual(source.readCount, 1)
        clock.advance(29)
        await monitor.tick()
        XCTAssertEqual(source.readCount, 1)
        clock.advance(1)
        await monitor.tick()
        XCTAssertEqual(source.readCount, 2)
        XCTAssertEqual(client.callCount, 0)

        source.state = validCreds
        clock.advance(30)
        await monitor.tick()
        XCTAssertEqual(client.callCount, 1)
        let state = await monitor.state
        XCTAssertEqual(state.status, .ok)
        let paused = await monitor.isPaused
        XCTAssertFalse(paused)
    }

    func testRefreshIsNotReentrant() async {
        let (monitor, _, client) = makeMonitor(validCreds, [.ok(snapshot())])
        let gate = CheckedContinuationBox()
        client.gate = gate
        let first = Task { await monitor.refresh() }
        for _ in 0..<400 where client.callCount == 0 {
            try? await Task.sleep(nanoseconds: 5_000_000)
        }
        XCTAssertEqual(client.callCount, 1, "first refresh should be in flight")
        await monitor.refresh()
        XCTAssertEqual(client.callCount, 1, "second refresh must return immediately")
        gate.open()
        await first.value
        let state = await monitor.state
        XCTAssertEqual(state.status, .ok)
    }
}

/// Collects states published by the monitor's handler.
final class StateBox: @unchecked Sendable {
    private let lock = NSLock()
    private var collected: [MonitorState] = []

    func append(_ state: MonitorState) {
        lock.lock(); defer { lock.unlock() }
        collected.append(state)
    }

    var states: [MonitorState] {
        lock.lock(); defer { lock.unlock() }
        return collected
    }
}
