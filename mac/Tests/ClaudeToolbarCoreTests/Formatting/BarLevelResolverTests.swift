import XCTest
@testable import ClaudeToolbarCore

final class BarLevelResolverTests: XCTestCase {
    func testThresholds() {
        XCTAssertEqual(BarLevelResolver.resolve(utilization: 0, warnThreshold: 70, critThreshold: 90), .ok)
        XCTAssertEqual(BarLevelResolver.resolve(utilization: 69.9, warnThreshold: 70, critThreshold: 90), .ok)
        XCTAssertEqual(BarLevelResolver.resolve(utilization: 70, warnThreshold: 70, critThreshold: 90), .warn)
        XCTAssertEqual(BarLevelResolver.resolve(utilization: 89.9, warnThreshold: 70, critThreshold: 90), .warn)
        XCTAssertEqual(BarLevelResolver.resolve(utilization: 90, warnThreshold: 70, critThreshold: 90), .crit)
        XCTAssertEqual(BarLevelResolver.resolve(utilization: 100, warnThreshold: 70, critThreshold: 90), .crit)
    }
}
