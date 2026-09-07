import Foundation

/// Injectable time source. Named to avoid clashing with the standard library's `Clock`.
public protocol ClockSource: Sendable {
    var now: Date { get }
}

public struct SystemClock: ClockSource {
    public init() {}
    public var now: Date { Date() }
}
