using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class SettingsValidatorTests
{
    [Fact]
    public void DefaultsAreAlreadyNormal()
    {
        var s = SettingsValidator.Normalize(AppSettings.CreateDefault());
        Assert.Equal("dark", s.Appearance.Preset);
        Assert.Equal("#CC1E1E1E", s.Appearance.Background);
        Assert.Equal(11, s.Appearance.FontSize);
        Assert.Equal(70, s.Appearance.WarnThreshold);
        Assert.Equal(90, s.Appearance.CritThreshold);
        Assert.Equal(60, s.Rows.BarWidth);
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.Equal(8, s.Behavior.TrayGapPx);
        Assert.True(s.Behavior.RunAtStartup);
        Assert.True(s.Behavior.HideInFullscreen);
    }

    [Fact]
    public void ClampsRanges()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.FontSize = 40;
        s.Appearance.CornerRadius = -3;
        s.Rows.BarWidth = 5;
        s.Behavior.RefreshIntervalSeconds = 1;
        s.Behavior.TrayGapPx = 500;
        SettingsValidator.Normalize(s);
        Assert.Equal(14, s.Appearance.FontSize);
        Assert.Equal(0, s.Appearance.CornerRadius);
        Assert.Equal(30, s.Rows.BarWidth);
        Assert.Equal(30, s.Behavior.RefreshIntervalSeconds);
        Assert.Equal(24, s.Behavior.TrayGapPx);
    }

    [Fact]
    public void WarnAtOrAboveCritResetsBoth()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.WarnThreshold = 95;
        s.Appearance.CritThreshold = 90;
        SettingsValidator.Normalize(s);
        Assert.Equal(70, s.Appearance.WarnThreshold);
        Assert.Equal(90, s.Appearance.CritThreshold);
    }

    [Fact]
    public void BadColorsFallBackToDefaults()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.Background = "red";
        s.Appearance.Text = "#12345";
        s.Appearance.BarOk = "#00FF00";
        SettingsValidator.Normalize(s);
        Assert.Equal("#CC1E1E1E", s.Appearance.Background);
        Assert.Equal("#FFF3F3F3", s.Appearance.Text);
        Assert.Equal("#FF00FF00", s.Appearance.BarOk);
    }

    [Fact]
    public void NullSectionsAreReplaced()
    {
        var s = SettingsJson.Deserialize("""{ "version": 1, "appearance": null }""");
        SettingsValidator.Normalize(s);
        Assert.NotNull(s.Appearance);
        Assert.NotNull(s.Rows);
        Assert.NotNull(s.Behavior);
    }

    [Theory]
    [InlineData("#CC1E1E1E", true)]
    [InlineData("#cc1e1e1e", true)]
    [InlineData("#1E1E1E", false)]
    [InlineData("CC1E1E1E", false)]
    [InlineData(null, false)]
    public void ValidatesColors(string? value, bool expected)
    {
        Assert.Equal(expected, SettingsValidator.IsValidColor(value));
    }
}
