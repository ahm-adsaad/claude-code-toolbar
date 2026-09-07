import Foundation
import ClaudeToolbarCore

/// Test clock. Thread-safe so it can be read from the monitor actor while tests advance it.
final class FakeClock: ClockSource, @unchecked Sendable {
    private let lock = NSLock()
    private var current: Date

    init(_ now: Date = Date(timeIntervalSince1970: 1_788_782_400)) {
        current = now
    }

    var now: Date {
        lock.lock(); defer { lock.unlock() }
        return current
    }

    func advance(_ seconds: TimeInterval) {
        lock.lock(); defer { lock.unlock() }
        current = current.addingTimeInterval(seconds)
    }

    func set(_ date: Date) {
        lock.lock(); defer { lock.unlock() }
        current = date
    }
}
