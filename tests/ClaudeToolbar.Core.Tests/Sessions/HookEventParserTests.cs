using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.Core.Tests.Sessions;

public class HookEventParserTests
{
    private static string Json(string name, string extra = "") =>
        $$"""{ "session_id": "abc123", "transcript_path": "/t", "cwd": "C:\\work\\my-repo", "hook_event_name": "{{name}}" {{extra}} }""";

    [Fact]
    public void ParsesEachKind()
    {
        Assert.Equal(new SessionEvent(SessionEventKind.Start, "abc123", "C:\\work\\my-repo", null, "startup"), HookEventParser.Parse(Json("SessionStart", ", \"source\": \"startup\"")));
        Assert.Equal(SessionEventKind.PromptSubmitted, HookEventParser.Parse(Json("UserPromptSubmit", ", \"prompt\": \"hi\""))!.Kind);
        Assert.Equal(SessionEventKind.Stopped, HookEventParser.Parse(Json("Stop", ", \"stop_hook_active\": false"))!.Kind);
        var failed = HookEventParser.Parse(Json("StopFailure", ", \"error_type\": \"rate_limit\""))!;
        Assert.Equal(SessionEventKind.Failed, failed.Kind);
        Assert.Equal("rate_limit", failed.Detail);
        var ended = HookEventParser.Parse(Json("SessionEnd", ", \"reason\": \"other\""))!;
        Assert.Equal(SessionEventKind.Ended, ended.Kind);
        Assert.Equal("other", ended.Detail);
    }

    [Theory]
    [InlineData("permission_prompt", SessionEventKind.NeedsAttention)]
    [InlineData("idle_prompt", SessionEventKind.NeedsAttention)]
    [InlineData("agent_needs_input", SessionEventKind.NeedsAttention)]
    [InlineData("elicitation_dialog", SessionEventKind.NeedsAttention)]
    [InlineData("elicitation_url_dialog", SessionEventKind.NeedsAttention)]
    [InlineData("auth_success", SessionEventKind.Info)]
    [InlineData("agent_completed", SessionEventKind.Info)]
    [InlineData("", SessionEventKind.Info)]
    public void NotificationTypesMap(string type, SessionEventKind expected)
    {
        var e = HookEventParser.Parse(Json("Notification", $", \"notification_type\": \"{type}\", \"message\": \"Claude needs your permission\""))!;
        Assert.Equal(expected, e.Kind);
        Assert.Equal("Claude needs your permission", e.Message);
        Assert.Equal(type, e.Detail);
    }

    [Fact]
    public void NotificationWithoutTypeIsInfo() =>
        Assert.Equal(SessionEventKind.Info, HookEventParser.Parse(Json("Notification", ", \"message\": \"x\""))!.Kind);

    [Fact]
    public void RejectsUnknownAndMalformed()
    {
        Assert.Null(HookEventParser.Parse(Json("PreToolUse")));
        Assert.Null(HookEventParser.Parse("{ \"hook_event_name\": \"Stop\" }"));
        Assert.Null(HookEventParser.Parse("{ \"hook_event_name\": \"Stop\", \"session_id\": \"\" }"));
        Assert.Null(HookEventParser.Parse("[1]"));
        Assert.Null(HookEventParser.Parse("not json"));
        Assert.Null(HookEventParser.Parse(""));
    }
}
