import XCTest
@testable import ClaudeToolbarCore

final class NotificationSettingsTests: XCTestCase {
    func testDefaults() {
        let n = AppSettings.createDefault().notifications
        XCTAssertTrue(n.enabled)
        XCTAssertEqual(n.port, 47831)
        XCTAssertTrue(n.sound)
    }

    func testRoundTripAndMissingSection() throws {
        var s = AppSettings.createDefault()
        s.notifications.port = 50000
        s.notifications.sound = false
        let back = try SettingsJSON.decode(try SettingsJSON.encode(s))
        XCTAssertEqual(back.notifications.port, 50000)
        XCTAssertFalse(back.notifications.sound)
        XCTAssertEqual(try SettingsJSON.decode("{}").notifications.port, 47831)
    }

    func testPortIsClamped() {
        var s = AppSettings.createDefault()
        s.notifications.port = 80
        XCTAssertEqual(SettingsValidator.normalize(s).notifications.port, 1024)
        s.notifications.port = 70000
        XCTAssertEqual(SettingsValidator.normalize(s).notifications.port, 65535)
    }

    func testClaudeSettingsPathFollowsTheConfigDir() {
        XCTAssertEqual(CredentialsPaths.claudeSettingsPath(claudeConfigDir: nil, homeDirectory: "/Users/sam"), "/Users/sam/.claude/settings.json")
        XCTAssertEqual(CredentialsPaths.claudeSettingsPath(claudeConfigDir: " /tmp/cfg ", homeDirectory: "/Users/sam"), "/tmp/cfg/settings.json")
    }
}
