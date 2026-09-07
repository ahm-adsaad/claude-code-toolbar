using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.Core.Tests.Sessions;

public class HooksConfigTests
{
    private const string Url = "http://127.0.0.1:47831/hook";

    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void UrlUsesThePort() => Assert.Equal(Url, HooksConfig.HookUrl(47831));

    [Fact]
    public void InstallIntoEmptyCreatesEveryEvent()
    {
        var json = HooksConfig.Install("", Url);
        var hooks = (JsonObject)Obj(json)["hooks"]!;
        Assert.Equal(HooksConfig.Events, hooks.Select(kv => kv.Key).ToList());
        foreach (var ev in HooksConfig.Events)
        {
            var handler = (JsonObject)((JsonArray)((JsonObject)((JsonArray)hooks[ev]!)[0]!)["hooks"]!)[0]!;
            Assert.Equal("http", handler["type"]!.GetValue<string>());
            Assert.Equal(Url, handler["url"]!.GetValue<string>());
            Assert.Equal(5, handler["timeout"]!.GetValue<int>());
        }
        Assert.True(HooksConfig.IsInstalled(json, Url));
        Assert.False(HooksConfig.IsInstalled("{}", Url));
        Assert.False(HooksConfig.IsInstalled("", Url));
    }

    [Fact]
    public void InstallIsIdempotentAndPreservesOtherContent()
    {
        const string existing = """
        {
          "model": "opus",
          "hooks": {
            "Stop": [ { "hooks": [ { "type": "command", "command": "say done" } ] } ],
            "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "lint" } ] } ]
          }
        }
        """;
        var once = HooksConfig.Install(existing, Url);
        var twice = HooksConfig.Install(once, Url);
        Assert.Equal(once, twice);
        var root = Obj(twice);
        Assert.Equal("opus", root["model"]!.GetValue<string>());
        var stop = (JsonArray)((JsonObject)root["hooks"]!)["Stop"]!;
        Assert.Equal(2, stop.Count);
        Assert.Equal("say done", ((JsonArray)((JsonObject)stop[0]!)["hooks"]!)[0]!["command"]!.GetValue<string>());
        Assert.NotNull(((JsonObject)root["hooks"]!)["PreToolUse"]);
        Assert.True(HooksConfig.IsInstalled(twice, Url));
    }

    [Fact]
    public void InstalledIsFalseWhenAnyEventIsMissing()
    {
        var json = HooksConfig.Install("", Url);
        var root = Obj(json);
        ((JsonObject)root["hooks"]!).Remove("SessionEnd");
        Assert.False(HooksConfig.IsInstalled(root.ToJsonString(), Url));
        Assert.False(HooksConfig.IsInstalled(HooksConfig.Install("", "http://127.0.0.1:50000/hook"), Url));
    }

    [Fact]
    public void RemoveLeavesOtherHooksAndDropsEmptyContainers()
    {
        const string existing = """{ "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "say done" } ] } ] }, "theme": "dark" }""";
        var installed = HooksConfig.Install(existing, Url);
        var removed = HooksConfig.Remove(installed, Url);
        var root = Obj(removed);
        Assert.Equal("dark", root["theme"]!.GetValue<string>());
        var hooks = (JsonObject)root["hooks"]!;
        Assert.Equal(["Stop"], hooks.Select(kv => kv.Key).ToList());
        Assert.Single((JsonArray)hooks["Stop"]!);
        Assert.False(HooksConfig.IsInstalled(removed, Url));

        var bare = HooksConfig.Remove(HooksConfig.Install("{}", Url), Url);
        Assert.Equal("{}", bare.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
    }

    [Fact]
    public void RemoveOnlyTouchesOurUrl()
    {
        var other = HooksConfig.Install("", "http://127.0.0.1:50000/hook");
        var both = HooksConfig.Install(other, Url);
        var removed = HooksConfig.Remove(both, Url);
        Assert.True(HooksConfig.IsInstalled(removed, "http://127.0.0.1:50000/hook"));
        Assert.False(HooksConfig.IsInstalled(removed, Url));
    }

    [Fact]
    public void AcceptsCommentsAndTrailingCommasButRejectsGarbage()
    {
        var json = HooksConfig.Install("{ // user settings\n \"model\": \"opus\", }", Url);
        Assert.True(HooksConfig.IsInstalled(json, Url));
        Assert.Throws<JsonException>(() => HooksConfig.Install("{ nope", Url));
        Assert.Throws<JsonException>(() => HooksConfig.Install("[]", Url));
    }
}
