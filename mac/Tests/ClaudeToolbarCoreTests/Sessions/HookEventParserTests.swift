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
            ("permission_prompt", .needsAttention), ("worker_permission_prompt", .needsAttention), ("agent_needs_input", .needsAttention),
            ("elicitation_dialog", .needsAttention), ("elicitation_url_dialog", .needsAttention),
            // "Claude is waiting for your input", sent a minute after every turn ends: the finished cue already said so.
            ("idle_prompt", .info), ("auth_success", .info), ("agent_completed", .info), ("", .info),
        ]
        for (type, expected) in cases {
            let e = HookEventParser.parse(json("Notification", #", "notification_type": "\#(type)", "message": "Claude needs your permission""#))
            XCTAssertEqual(e?.kind, expected, type)
            XCTAssertEqual(e?.message, "Claude needs your permission")
            XCTAssertEqual(e?.detail, type)
        }
        XCTAssertEqual(HookEventParser.parse(json("Notification", #", "message": "x""#))?.kind, .info)
    }

    func testStopCountsTheAgentsStillRunningInTheBackground() {
        let tasks = #"""
            , "background_tasks": [
                { "id": "a1", "type": "subagent", "status": "running", "description": "Implement task 8", "agent_type": "general-purpose" },
                { "id": "a2", "type": "subagent", "status": "pending", "agent_type": "Explore" },
                { "id": "a3", "type": "subagent", "status": "completed", "agent_type": "Explore" },
                { "id": "a4", "type": "local_agent", "status": "running" },
                { "id": "a5", "type": "remote_agent", "status": "killed" },
                { "id": "sh", "type": "local_bash", "status": "running", "description": "npm run dev" },
                { "id": "d", "type": "dream", "status": "running" },
                "not an object",
                { "type": "subagent" }
            ], "session_crons": []
            """#
        let stopped = HookEventParser.parse(json("Stop", tasks))
        XCTAssertEqual(stopped?.kind, .stopped)
        XCTAssertEqual(stopped?.runningAgents, 3)
        XCTAssertEqual(HookEventParser.parse(json("Stop", #", "background_tasks": []"#))?.runningAgents, 0)
        XCTAssertEqual(HookEventParser.parse(json("Stop"))?.runningAgents, 0)
        XCTAssertEqual(HookEventParser.parse(json("Stop", #", "background_tasks": "nope""#))?.runningAgents, 0)
        // Only Stop carries the count; every other event reports none.
        XCTAssertEqual(HookEventParser.parse(json("UserPromptSubmit", tasks))?.runningAgents, 0)
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
