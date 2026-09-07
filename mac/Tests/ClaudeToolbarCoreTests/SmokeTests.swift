import XCTest
@testable import ClaudeToolbarCore

final class SmokeTests: XCTestCase {
    func testVersionIsSet() {
        XCTAssertEqual(CoreInfo.version, "0.1.0")
    }

    func testAsyncWorks() async {
        let value = await Task { 41 + 1 }.value
        XCTAssertEqual(value, 42)
    }
}
