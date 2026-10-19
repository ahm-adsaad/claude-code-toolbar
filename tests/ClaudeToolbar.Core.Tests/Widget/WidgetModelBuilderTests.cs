using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Pace;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Widget;

public class WidgetModelBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly CredentialsState Creds = new CredentialsState.Valid("p", "t", Now.AddHours(7), "max");

    private static UsageSnapshot Snap() => new(
        new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
        new UsageWindow(75, Now.AddDays(3).AddHours(4)),
        new UsageWindow(95, Now.AddDays(3)),
        null,
        null,
        Now);

    private static Dictionary<UsageLimit, PaceEstimate> Pace(UsageLimit limit, PaceVerdict verdict, TimeSpan workLeft) =>
        new() { [limit] = new PaceEstimate(verdict, workLeft, null, 1, TimeSpan.FromHours(9), PaceBasis.Work) };

    private static MonitorState WithFable(UsageStatus status = UsageStatus.Ok) =>
        new(status, Snap() with { SevenDayFable = new UsageWindow(72, Now.AddDays(3)) }, Now, null, Creds);

    [Fact]
    public void Warning_WhenRunsOutFirst()
    {
        var m = WidgetModelBuilder.Build(WithFable(), AppSettings.CreateDefault(), Now, Pace(UsageLimit.SevenDayFable, PaceVerdict.RunsOutFirst, new TimeSpan(6, 12, 0)));
        var fable = m.Rows[^1];
        Assert.Equal("~6h work", fable.Warning);
        Assert.Equal(BarLevel.Warn, fable.WarningLevel);
        Assert.Equal("3d 0h", fable.TimeText);
        Assert.Null(m.Rows[0].Warning);
    }

    [Fact]
    public void Warning_CriticalUnderThirtyMinutes()
    {
        var m = WidgetModelBuilder.Build(WithFable(), AppSettings.CreateDefault(), Now, Pace(UsageLimit.FiveHour, PaceVerdict.RunsOutFirst, TimeSpan.FromMinutes(20)));
        Assert.Equal("~20m work", m.Rows[0].Warning);
        Assert.Equal(BarLevel.Crit, m.Rows[0].WarningLevel);
    }

    [Fact]
    public void NoWarning_WhenItLasts()
    {
        var m = WidgetModelBuilder.Build(WithFable(), AppSettings.CreateDefault(), Now, Pace(UsageLimit.SevenDayFable, PaceVerdict.Lasts, TimeSpan.FromHours(90)));
        Assert.Null(m.Rows[^1].Warning);
    }

    [Fact]
    public void NoWarning_WhenLoginExpired()
    {
        var m = WidgetModelBuilder.Build(WithFable(UsageStatus.Expired), AppSettings.CreateDefault(), Now, Pace(UsageLimit.SevenDayFable, PaceVerdict.RunsOutFirst, TimeSpan.FromHours(6)));
        Assert.Null(m.Rows[^1].Warning);
    }

    [Fact]
    public void OkStateBuildsTwoDefaultRows()
    {
        var state = new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds);
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.Equal(2, m.Rows.Count);
        Assert.Equal(new WidgetRow(UsageLimit.FiveHour, "5h", "5h", 42, "42%", "2h 13m", BarLevel.Ok), m.Rows[0]);
        Assert.Equal(new WidgetRow(UsageLimit.SevenDay, "7d", "7d", 75, "75%", "3d 4h", BarLevel.Warn), m.Rows[1]);
        Assert.False(m.Dimmed);
        Assert.False(m.ShowStaleDot);
        Assert.Null(m.Notice);
    }

    [Fact]
    public void PerModelRowsFollowSettings()
    {
        var s = AppSettings.CreateDefault();
        s.Rows.ShowSevenDayOpus = true;
        s.Rows.ShowSevenDaySonnet = true;
        var state = new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds);
        var m = WidgetModelBuilder.Build(state, s, Now);
        Assert.Equal(4, m.Rows.Count);
        Assert.Equal("7d Opus", m.Rows[2].Label);
        Assert.Equal(BarLevel.Crit, m.Rows[2].Level);
        Assert.Equal("7d Sonnet", m.Rows[3].Label);
        Assert.Equal("—", m.Rows[3].PercentText);
    }

    [Fact]
    public void FableRow_ShownWithShortLabel()
    {
        var snap = Snap() with { SevenDayFable = new UsageWindow(72, Now.AddDays(3)) };
        var m = WidgetModelBuilder.Build(new MonitorState(UsageStatus.Ok, snap, Now, null, Creds), AppSettings.CreateDefault(), Now);
        var fable = m.Rows[^1];
        Assert.Equal(UsageLimit.SevenDayFable, fable.Limit);
        Assert.Equal("7d Fable", fable.Label);
        Assert.Equal("Fa", fable.ShortLabel);
        Assert.Equal(BarLevel.Warn, fable.Level);
    }

    [Fact]
    public void FableRow_HiddenWhenApiHasNone()
    {
        var s = AppSettings.CreateDefault();
        s.Rows.ShowSevenDaySonnet = true;
        var m = WidgetModelBuilder.Build(new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds), s, Now);
        Assert.DoesNotContain(m.Rows, r => r.Limit == UsageLimit.SevenDayFable);
        Assert.Equal("—", m.Rows.Single(r => r.Limit == UsageLimit.SevenDaySonnet).PercentText);
    }

    [Fact]
    public void ShortLabels()
    {
        var s = AppSettings.CreateDefault();
        s.Rows.ShowSevenDayOpus = true;
        s.Rows.ShowSevenDaySonnet = true;
        var snap = Snap() with { SevenDayFable = new UsageWindow(1, null) };
        var m = WidgetModelBuilder.Build(new MonitorState(UsageStatus.Ok, snap, Now, null, Creds), s, Now);
        Assert.Equal(["5h", "7d", "Op", "So", "Fa"], m.Rows.Select(r => r.ShortLabel));
    }

    [Fact]
    public void LoadingShowsDashes()
    {
        var state = new MonitorState(UsageStatus.Loading, null, null, null, Creds);
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.All(m.Rows, r => Assert.Equal("—", r.PercentText));
        Assert.All(m.Rows, r => Assert.Equal(0.0, r.Utilization));
    }

    [Fact]
    public void StaleShowsDot()
    {
        var state = new MonitorState(UsageStatus.Stale, Snap(), Now, "net", Creds);
        Assert.True(WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now).ShowStaleDot);
    }

    [Fact]
    public void ExpiredDimsAndReplacesFirstTime()
    {
        var state = new MonitorState(UsageStatus.Expired, Snap(), Now, "expired", Creds);
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.True(m.Dimmed);
        Assert.Equal(WidgetModelBuilder.RunClaudeHint, m.Rows[0].TimeText);
        Assert.Equal("3d 4h", m.Rows[1].TimeText);
    }

    [Fact]
    public void NoCredentialsShowsSignInNotice()
    {
        var state = new MonitorState(UsageStatus.NoCredentials, null, null, null, new CredentialsState.Missing("p"));
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.Empty(m.Rows);
        Assert.Equal(WidgetModelBuilder.SignInNotice, m.Notice);
    }

    [Fact]
    public void ThresholdsComeFromSettings()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.WarnThreshold = 40;
        s.Appearance.CritThreshold = 50;
        var state = new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds);
        var m = WidgetModelBuilder.Build(state, s, Now);
        Assert.Equal(BarLevel.Warn, m.Rows[0].Level);
        Assert.Equal(BarLevel.Crit, m.Rows[1].Level);
    }
}
