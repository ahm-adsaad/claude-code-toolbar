import Foundation
import ClaudeToolbarCore

final class FakeCredentialsSource: CredentialsSource, @unchecked Sendable {
    private let lock = NSLock()
    private var current: CredentialsState
    private var reads = 0

    let sourceName: String

    init(_ state: CredentialsState, sourceName: String = "fake") {
        current = state
        self.sourceName = sourceName
    }

    var state: CredentialsState {
        get { lock.lock(); defer { lock.unlock() }; return current }
        set { lock.lock(); defer { lock.unlock() }; current = newValue }
    }

    var readCount: Int {
        lock.lock(); defer { lock.unlock() }
        return reads
    }

    func read() -> CredentialsState {
        lock.lock(); defer { lock.unlock() }
        reads += 1
        return current
    }
}
