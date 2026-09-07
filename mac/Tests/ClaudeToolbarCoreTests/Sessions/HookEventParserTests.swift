import XCTest
@testable import ClaudeToolbarCore

final class HookEventParserTests: XCTestCase {
    private func json(_ name: String, _ extra: String = "") -> String {
        #"{ "session_id": "abc123", "transcript_path": "/t", "cwd": "/work/my-repo", "hook_event_name": "\#(name)" \#(extra) }"#
    }

    func testParsesEachKind() {
        XCTAssertEqual(HookEventParser.parse(json("SessionStart", #", "source": "startup""#)),
                       SessionEvent(kind: .start, sessionId: "abc123", cwd: "/work/my-repo", message: nil, detail: "startup"))
        XCTAssertEqual(HookEventParser.parse(json("UserPromptSubmit", #", "prompt": "hi""#))?.kind, .promptSubmitted)
        XCTAssertEqual(HookEventParser.parse(json("Stop", #", "stop_hook_active": false"#))?.kind, .stopped)
        let failed = HookEventParser.parse(json("StopFailure", #", "error_type": "rate_limit""#))
        XCTAssertEqual(failed?.kind, .failed)
        XCTAssertEqual(failed?.detail, "rate_limit")
        let ended = HookEventParser.parse(json("SessionEnd", #", "reason": "other""#))
        XCTAssertEqual(ended?.kind, .ended)
        XCTAssertEqual(ended?.detail, "other")
    }

    func testNotificationTypesMap() {
        let cases: [(String, SessionEventKind)] = [
            ("permission_prompt", .needsAttention), ("idle_prompt", .needsAttention), ("agent_needs_input", .needsAttention),
            ("elicitation_dialog", .needsAttention), ("elicitation_url_dialog", .needsAttention),
            ("auth_success", .info), ("agent_completed", .info), ("", .info),
        ]
        for (type, expected) in cases {
            let e = HookEventParser.parse(json("Notification", #", "notification_type": "\#(type)", "message": "Claude needs your permission""#))
            XCTAssertEqual(e?.kind, expected, type)
            XCTAssertEqual(e?.message, "Claude needs your permission")
            XCTAssertEqual(e?.detail, type)
        }
        XCTAssertEqual(HookEventParser.parse(json("Notification", #", "message": "x""#))?.kind, .info)
    }

    func testRejectsUnknownAndMalformed() {
        XCTAssertNil(HookEventParser.parse(json("PreToolUse")))
        XCTAssertNil(HookEventParser.parse(#"{ "hook_event_name": "Stop" }"#))
        XCTAssertNil(HookEventParser.parse(#"{ "hook_event_name": "Stop", "session_id": "" }"#))
        XCTAssertNil(HookEventParser.parse("[1]"))
        XCTAssertNil(HookEventParser.parse("not json"))
        XCTAssertNil(HookEventParser.parse(""))
    }
}
