using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Usage;

public class UsageResponseParserTests
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private const string FullPayload = """
        {
          "five_hour":        { "utilization": 42.0, "resets_at": "2026-09-03T14:00:00+00:00" },
          "seven_day":        { "utilization": 18.0, "resets_at": "2026-09-06T08:00:00+00:00" },
          "seven_day_opus":   { "utilization": 5.0,  "resets_at": "2026-09-06T08:00:00+00:00" },
          "seven_day_sonnet": null,
          "extra_usage":      { "is_enabled": false }
        }
        """;

    [Fact]
    public void ParsesFullPayload()
    {
        var result = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(FullPayload, FetchedAt));
        var s = result.Snapshot;
        Assert.Equal(42.0, s.FiveHour!.Utilization);
        Assert.Equal(new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero), s.FiveHour.ResetsAt);
        Assert.Equal(18.0, s.SevenDay!.Utilization);
        Assert.Equal(5.0, s.SevenDayOpus!.Utilization);
        Assert.Null(s.SevenDaySonnet);
        Assert.Equal(FetchedAt, s.FetchedAt);
    }

    [Fact]
    public void MissingPerModelFieldsAreNull()
    {
        var json = """{ "five_hour": { "utilization": 1, "resets_at": "2026-09-03T14:00:00Z" }, "seven_day": { "utilization": 2, "resets_at": "2026-09-06T08:00:00Z" } }""";
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt));
        Assert.Null(ok.Snapshot.SevenDayOpus);
        Assert.Null(ok.Snapshot.SevenDaySonnet);
    }

    [Fact]
    public void Fable_ComesFromScopedWeeklyLimit()
    {
        const string json = """
            {
              "seven_day": { "utilization": 27.0, "resets_at": "2026-09-10T16:00:00+00:00" },
              "limits": [
                { "kind": "weekly_all", "percent": 27, "resets_at": "2026-09-10T16:00:00+00:00", "scope": null },
                { "kind": "weekly_scoped", "percent": 8, "resets_at": "2026-09-10T16:00:00Z",
                  "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null } }
              ]
            }
            """;
        var s = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt)).Snapshot;
        Assert.Equal(8, s.SevenDayFable!.Utilization);
        Assert.Equal(new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.Zero), s.SevenDayFable.ResetsAt);
        Assert.Equal(27, s.SevenDay!.Utilization);
    }

    [Fact]
    public void Fable_PrefersItsOwnFieldAndIsNullWhenAbsent()
    {
        const string own = """{ "seven_day_fable": { "utilization": 12 }, "limits": [ { "kind": "weekly_scoped", "percent": 8, "scope": { "model": { "display_name": "Fable" } } } ] }""";
        Assert.Equal(12, Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(own, FetchedAt)).Snapshot.SevenDayFable!.Utilization);
        const string other = """{ "limits": [ { "kind": "weekly_scoped", "percent": 8, "scope": { "model": { "display_name": "Opus" } } }, { "kind": "session", "percent": 50 } ] }""";
        Assert.Null(Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(other, FetchedAt)).Snapshot.SevenDayFable);
        Assert.Null(Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(FullPayload, FetchedAt)).Snapshot.SevenDayFable);
    }

    [Theory]
    [InlineData(150.0, 100.0)]
    [InlineData(-5.0, 0.0)]
    public void ClampsUtilization(double raw, double expected)
    {
        var json = $$"""{ "five_hour": { "utilization": {{raw}}, "resets_at": "2026-09-03T14:00:00Z" } }""";
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt));
        Assert.Equal(expected, ok.Snapshot.FiveHour!.Utilization);
    }

    [Fact]
    public void NullResetKeepsUtilization()
    {
        var json = """{ "five_hour": { "utilization": 0, "resets_at": null } }""";
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt));
        Assert.Equal(0.0, ok.Snapshot.FiveHour!.Utilization);
        Assert.Null(ok.Snapshot.FiveHour.ResetsAt);
    }

    [Fact]
    public void EmptyObjectIsOkWithNoWindows()
    {
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse("{}", FetchedAt));
        Assert.Empty(ok.Snapshot.Windows);
        Assert.Null(ok.Snapshot.NextReset);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void BadJsonFails(string json)
    {
        Assert.IsType<UsageResult.Failed>(UsageResponseParser.Parse(json, FetchedAt));
    }
}
