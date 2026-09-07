import XCTest
@testable import ClaudeToolbarCore

final class CredentialsPathsTests: XCTestCase {
    func testDefaultUnderHome() {
        let path = CredentialsPaths.resolve(claudeConfigDir: nil, homeDirectory: "/Users/sam")
        XCTAssertEqual(path, "/Users/sam/.claude/.credentials.json")
    }

    func testBlankOverrideIsIgnored() {
        XCTAssertEqual(CredentialsPaths.resolve(claudeConfigDir: "   ", homeDirectory: "/Users/sam"), "/Users/sam/.claude/.credentials.json")
    }

    func testOverrideWins() {
        XCTAssertEqual(CredentialsPaths.resolve(claudeConfigDir: " /tmp/cfg ", homeDirectory: "/Users/sam"), "/tmp/cfg/.credentials.json")
    }
}
