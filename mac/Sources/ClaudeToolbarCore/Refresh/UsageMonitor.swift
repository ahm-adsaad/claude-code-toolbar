import Foundation

/// Owns the fetch loop state machine. The host calls `tick()` about once a second.
public actor UsageMonitor {
    public static let pausedRecheckInterval: TimeInterval = 30

    public private(set) var state: MonitorState

    private let scheduler: RefreshScheduler
    private let credentials: any CredentialsSource
    private let client: any UsageClient
    private let clock: any ClockSource
    private var handler: (@Sendable (MonitorState) -> Void)?
    private var refreshing = false
    private var resetTriggeredFor: Date?
    private var pausedSince: Date?
    private var cachedCredentials: CredentialsState?

    public init(credentials: any CredentialsSource, client: any UsageClient, clock: any ClockSource, intervalSeconds: Int = 60) {
        self.credentials = credentials
        self.client = client
        self.clock = clock
        scheduler = RefreshScheduler(clock: clock, intervalSeconds: intervalSeconds)
        state = .initial(credentials: .missing(source: credentials.sourceName))
    }

    public func setStateHandler(_ handler: @escaping @Sendable (MonitorState) -> Void) {
        self.handler = handler
    }

    public var isPaused: Bool { scheduler.isPaused }
    public var nextDue: Date? { scheduler.nextDue }
    public var intervalSeconds: Int { scheduler.intervalSeconds }

    public func setIntervalSeconds(_ seconds: Int) {
        guard seconds != scheduler.intervalSeconds else { return }
        scheduler.intervalSeconds = seconds
        scheduler.requestImmediate()
    }

    public func requestRefresh() {
        scheduler.requestImmediate()
    }

    public func tick() async {
        let now = clock.now
        if let reset = state.snapshot?.nextReset, reset <= now, resetTriggeredFor != reset {
            resetTriggeredFor = reset
            scheduler.requestImmediate()
        }
        if scheduler.isPaused, let since = pausedSince, now.timeIntervalSince(since) >= Self.pausedRecheckInterval {
            pausedSince = now
            scheduler.requestImmediate()
        }
        guard scheduler.isDue(at: now) else { return }
        await refresh()
    }

    public func refresh() async {
        if refreshing { return }
        refreshing = true
        defer { refreshing = false }

        // Reading the Keychain launches /usr/bin/security, so a token that is still valid is reused rather than
        // read again on every refresh. A rejected cached token falls through to a fresh read: Claude Code may
        // have signed in again since.
        if let cached = reusableCredentials, await attempt(with: cached, cached: true) { return }
        let source = credentials
        let creds = await Task.detached { source.read() }.value
        _ = await attempt(with: creds, cached: false)
    }

    private var reusableCredentials: CredentialsState? {
        guard let cached = cachedCredentials, let expiresAt = cached.expiresAt,
              clock.now < expiresAt.addingTimeInterval(-CredentialsPayloadParser.expiryMargin) else { return nil }
        return cached
    }

    /// False only when a cached token was rejected, telling the caller to read the credentials afresh.
    private func attempt(with creds: CredentialsState, cached: Bool) async -> Bool {
        switch creds {
        case .missing:
            cachedCredentials = nil
            pause()
            publish(status: .noCredentials, message: "Credentials not found", credentials: creds)
        case .invalid(_, let reason):
            cachedCredentials = nil
            pause()
            publish(status: .noCredentials, message: reason, credentials: creds)
        case .expired:
            cachedCredentials = nil
            pause()
            publish(status: .expired, message: "Login expired", credentials: creds)
        case .valid(_, let token, _, _):
            let result = await client.fetch(accessToken: token)
            if case .unauthorized = result {
                cachedCredentials = nil
                if cached { return false }
            } else {
                cachedCredentials = creds
            }
            apply(result, credentials: creds)
        }
        return true
    }

    private func apply(_ result: UsageResult, credentials creds: CredentialsState) {
        switch result {
        case .ok(let snapshot):
            pausedSince = nil
            scheduler.onSuccess(nextReset: snapshot.nextReset)
            publish(MonitorState(status: .ok, snapshot: snapshot, lastSuccess: snapshot.fetchedAt, message: nil, credentials: creds))
        case .unauthorized:
            pause()
            publish(status: .expired, message: "Token rejected", credentials: creds)
        case .rateLimited(let retryAfter):
            scheduler.onFailure(retryAfter: retryAfter)
            publish(status: degradedStatus, message: "Rate limited", credentials: creds)
        case .failed(let message):
            scheduler.onFailure(retryAfter: nil)
            publish(status: degradedStatus, message: message, credentials: creds)
        }
    }

    private var degradedStatus: UsageStatus { state.snapshot == nil ? .loading : .stale }

    private func pause() {
        scheduler.pause()
        pausedSince = clock.now
    }

    private func publish(status: UsageStatus, message: String?, credentials: CredentialsState) {
        var next = state
        next.status = status
        next.message = message
        next.credentials = credentials
        publish(next)
    }

    private func publish(_ next: MonitorState) {
        state = next
        handler?(next)
    }
}
