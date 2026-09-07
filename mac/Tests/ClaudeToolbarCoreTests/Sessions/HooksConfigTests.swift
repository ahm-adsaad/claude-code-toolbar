import XCTest
@testable import ClaudeToolbarCore

final class HooksConfigTests: XCTestCase {
    private let url = "http://127.0.0.1:47831/hook"

    private func object(_ json: String) throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(with: Data(json.utf8)) as? [String: Any])
    }

    private func groups(_ root: [String: Any], _ event: String) throws -> [[String: Any]] {
        let hooks = try XCTUnwrap(root["hooks"] as? [String: Any])
        return try XCTUnwrap(hooks[event] as? [[String: Any]])
    }

    func testUrlUsesThePort() {
        XCTAssertEqual(HooksConfig.hookUrl(port: 47831), url)
    }

    func testInstallIntoEmptyCreatesEveryEvent() throws {
        let json = try HooksConfig.install("", url: url)
        let root = try object(json)
        let hooks = try XCTUnwrap(root["hooks"] as? [String: Any])
        XCTAssertEqual(Set(hooks.keys), Set(HooksConfig.events))
        for event in HooksConfig.events {
            let handlers = try XCTUnwrap(try groups(root, event)[0]["hooks"] as? [[String: Any]])
            XCTAssertEqual(handlers[0]["type"] as? String, "http")
            XCTAssertEqual(handlers[0]["url"] as? String, url)
            XCTAssertEqual(handlers[0]["timeout"] as? Int, 5)
        }
        XCTAssertTrue(try HooksConfig.isInstalled(json, url: url))
        XCTAssertFalse(try HooksConfig.isInstalled("{}", url: url))
        XCTAssertFalse(try HooksConfig.isInstalled("", url: url))
    }

    func testInstallIsIdempotentAndPreservesOtherContent() throws {
        let existing = """
        { "model": "opus", "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "say done" } ] } ],
          "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "lint" } ] } ] } }
        """
        let once = try HooksConfig.install(existing, url: url)
        let twice = try HooksConfig.install(once, url: url)
        XCTAssertEqual(once, twice)
        let root = try object(twice)
        XCTAssertEqual(root["model"] as? String, "opus")
        let stop = try groups(root, "Stop")
        XCTAssertEqual(stop.count, 2)
        XCTAssertEqual((stop[0]["hooks"] as? [[String: Any]])?[0]["command"] as? String, "say done")
        XCTAssertNotNil((root["hooks"] as? [String: Any])?["PreToolUse"])
        XCTAssertTrue(try HooksConfig.isInstalled(twice, url: url))
    }

    func testInstalledIsFalseWhenAnyEventIsMissing() throws {
        let other = try HooksConfig.install("", url: "http://127.0.0.1:50000/hook")
        XCTAssertFalse(try HooksConfig.isInstalled(other, url: url))
        let json = try HooksConfig.install("", url: url)
        var root = try object(json)
        var hooks = try XCTUnwrap(root["hooks"] as? [String: Any])
        hooks.removeValue(forKey: "SessionEnd")
        root["hooks"] = hooks
        let trimmed = String(decoding: try JSONSerialization.data(withJSONObject: root), as: UTF8.self)
        XCTAssertFalse(try HooksConfig.isInstalled(trimmed, url: url))
    }

    func testRemoveLeavesOtherHooksAndDropsEmptyContainers() throws {
        let existing = #"{ "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "say done" } ] } ] }, "theme": "dark" }"#
        let installed = try HooksConfig.install(existing, url: url)
        let removed = try HooksConfig.remove(installed, url: url)
        let root = try object(removed)
        XCTAssertEqual(root["theme"] as? String, "dark")
        let hooks = try XCTUnwrap(root["hooks"] as? [String: Any])
        XCTAssertEqual(Array(hooks.keys), ["Stop"])
        XCTAssertEqual(try groups(root, "Stop").count, 1)
        XCTAssertFalse(try HooksConfig.isInstalled(removed, url: url))

        let bare = try HooksConfig.remove(try HooksConfig.install("{}", url: url), url: url)
        XCTAssertTrue(try object(bare).isEmpty)
    }

    func testRemoveOnlyTouchesOurUrl() throws {
        let other = "http://127.0.0.1:50000/hook"
        let both = try HooksConfig.install(try HooksConfig.install("", url: other), url: url)
        let removed = try HooksConfig.remove(both, url: url)
        XCTAssertTrue(try HooksConfig.isInstalled(removed, url: other))
        XCTAssertFalse(try HooksConfig.isInstalled(removed, url: url))
    }

    func testRejectsGarbage() {
        XCTAssertThrowsError(try HooksConfig.install("{ nope", url: url))
        XCTAssertThrowsError(try HooksConfig.install("[]", url: url))
        XCTAssertThrowsError(try HooksConfig.isInstalled("{ nope", url: url))
    }
}
