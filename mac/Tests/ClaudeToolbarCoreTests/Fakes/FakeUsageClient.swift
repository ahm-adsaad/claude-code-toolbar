import Foundation
import ClaudeToolbarCore

final class FakeUsageClient: UsageClient, @unchecked Sendable {
    private let lock = NSLock()
    private var queue: [UsageResult]
    private var tokens: [String] = []
    /// When set, `fetch` waits on it so tests can observe an in-flight refresh.
    var gate: CheckedContinuationBox?

    init(_ results: [UsageResult] = []) {
        queue = results
    }

    var callCount: Int {
        lock.lock(); defer { lock.unlock() }
        return tokens.count
    }

    var lastToken: String? {
        lock.lock(); defer { lock.unlock() }
        return tokens.last
    }

    func enqueue(_ result: UsageResult) {
        lock.lock(); defer { lock.unlock() }
        queue.append(result)
    }

    func fetch(accessToken: String) async -> UsageResult {
        lock.lock()
        tokens.append(accessToken)
        let result: UsageResult = queue.isEmpty ? .failed("no fake result queued") : (queue.count == 1 ? queue[0] : queue.removeFirst())
        let gate = self.gate
        lock.unlock()
        if let gate {
            await gate.wait()
        }
        return result
    }
}

/// A one-shot async gate: `wait()` suspends until `open()` is called.
final class CheckedContinuationBox: @unchecked Sendable {
    private let lock = NSLock()
    private var opened = false
    private var waiters: [CheckedContinuation<Void, Never>] = []

    func wait() async {
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            lock.lock()
            if opened {
                lock.unlock()
                continuation.resume()
            } else {
                waiters.append(continuation)
                lock.unlock()
            }
        }
    }

    func open() {
        lock.lock()
        opened = true
        let pending = waiters
        waiters.removeAll()
        lock.unlock()
        pending.forEach { $0.resume() }
    }
}
