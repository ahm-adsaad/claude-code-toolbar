import XCTest
@testable import ClaudeToolbarCore

final class PaceStoreTests: XCTestCase {
    private let t0 = Date(timeIntervalSince1970: 1_792_400_400.25)
    private var dir: URL!
    private var path: String { dir.appendingPathComponent("pace.json").path }

    override func setUpWithError() throws {
        dir = FileManager.default.temporaryDirectory.appendingPathComponent("ct-pace-\(UUID().uuidString)")
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: dir)
    }

    private func write(_ text: String) throws {
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try Data(text.utf8).write(to: URL(fileURLWithPath: path))
    }

    func testSaveThenLoadRoundTrips() throws {
        var state = PaceState()
        state.workMinutes = 72.5
        state.activeMinutes = 40.25
        state.limits[.fiveHour] = PaceLimitHistory(
            resetsAt: t0.addingTimeInterval(5 * 3600),
            points: [PacePoint(at: t0, workMinutes: 0, activeMinutes: 0, utilization: 10),
                     PacePoint(at: t0.addingTimeInterval(600), workMinutes: 12.5, activeMinutes: 10, utilization: 15.5)],
            latest: PacePoint(at: t0.addingTimeInterval(1200), workMinutes: 30, activeMinutes: 20, utilization: 15.5))
        state.limits[.sevenDayFable] = PaceLimitHistory(resetsAt: nil, points: [PacePoint(at: t0, workMinutes: 0, activeMinutes: 0, utilization: 61)],
                                                        latest: PacePoint(at: t0, workMinutes: 0, activeMinutes: 0, utilization: 61))
        let store = PaceStore(path: path)
        try store.save(state)
        XCTAssertEqual(store.load(), state)
        let text = try String(contentsOfFile: path, encoding: .utf8)
        XCTAssertTrue(text.contains("\"fiveHour\""), text)
    }

    func testLoadMissingFileReturnsNil() {
        XCTAssertNil(PaceStore(path: path).load())
    }

    func testLoadCorruptFileReturnsNil() throws {
        try write(#"{"limits":"#)
        XCTAssertNil(PaceStore(path: path).load())
    }

    func testLoadFutureVersionReturnsNil() throws {
        try write(#"{"version": 99, "workMinutes": 5, "activeMinutes": 0, "limits": {}}"#)
        XCTAssertNil(PaceStore(path: path).load())
    }
}
