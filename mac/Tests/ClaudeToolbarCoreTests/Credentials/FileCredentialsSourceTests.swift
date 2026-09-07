import XCTest
@testable import ClaudeToolbarCore

final class FileCredentialsSourceTests: XCTestCase {
    private var directory: URL!
    private let clock = FakeClock()

    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent("ct-creds-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: directory)
    }

    private func write(_ text: String) -> String {
        let path = directory.appendingPathComponent(".credentials.json").path
        _ = FileManager.default.createFile(atPath: path, contents: Data(text.utf8))
        return path
    }

    func testMissingFile() {
        let path = directory.appendingPathComponent("nope.json").path
        let state = FileCredentialsSource(path: path, clock: clock).read()
        guard case .missing(let source) = state else { return XCTFail("expected missing") }
        XCTAssertEqual(source, path)
    }

    func testValidFile() {
        let expires = Int64(clock.now.timeIntervalSince1970 * 1000) + 3_600_000
        let path = write(#"{ "claudeAiOauth": { "accessToken": "tok", "expiresAt": \#(expires), "subscriptionType": "pro" } }"#)
        let source = FileCredentialsSource(path: path, clock: clock)
        XCTAssertEqual(source.sourceName, path)
        guard case .valid(let src, let token, _, let sub) = source.read() else { return XCTFail("expected valid") }
        XCTAssertEqual(src, path)
        XCTAssertEqual(token, "tok")
        XCTAssertEqual(sub, "pro")
    }

    func testInvalidFile() {
        let path = write("{ nope")
        guard case .invalid(_, let reason) = FileCredentialsSource(path: path, clock: clock).read() else { return XCTFail("expected invalid") }
        XCTAssertEqual(reason, "Invalid JSON")
    }
}
