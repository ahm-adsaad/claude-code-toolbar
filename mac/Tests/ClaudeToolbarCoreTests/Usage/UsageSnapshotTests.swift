import XCTest
@testable import ClaudeToolbarCore

final class UsageSnapshotTests: XCTestCase {
    func testWindowsAndNextResetSkipNils() {
        let t = Date(timeIntervalSince1970: 1_788_782_400)
        let s = UsageSnapshot(
            fiveHour: UsageWindow(utilization: 1, resetsAt: t.addingTimeInterval(3 * 3600)),
            sevenDay: UsageWindow(utilization: 2, resetsAt: t.addingTimeInterval(2 * 86_400)),
            sevenDayOpus: UsageWindow(utilization: 3, resetsAt: nil),
            sevenDaySonnet: nil,
            fetchedAt: t)
        XCTAssertEqual(s.windows.count, 3)
        XCTAssertEqual(s.nextReset, t.addingTimeInterval(3 * 3600))
    }

    func testNextResetIsNilWithoutDates() {
        let t = Date(timeIntervalSince1970: 1_788_782_400)
        let s = UsageSnapshot(fiveHour: UsageWindow(utilization: 1, resetsAt: nil), sevenDay: nil, sevenDayOpus: nil, sevenDaySonnet: nil, fetchedAt: t)
        XCTAssertNil(s.nextReset)
    }
}
