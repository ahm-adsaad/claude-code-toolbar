using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class PresetsTests
{
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("claude")]
    [InlineData("mono")]
    public void EveryPresetProducesValidColors(string name)
    {
        var a = new AppearanceSettings();
        Assert.True(Presets.TryApply(name, a));
        Assert.Equal(name, a.Preset);
        foreach (var c in new[] { a.Background, a.Text, a.BarTrack, a.BarOk, a.BarWarn, a.BarCrit })
            Assert.True(SettingsValidator.IsValidColor(c), c);
    }

    [Fact]
    public void UnknownPresetIsRejected()
    {
        var a = new AppearanceSettings();
        Assert.False(Presets.TryApply("neon", a));
        Assert.Equal("dark", a.Preset);
    }

    [Fact]
    public void NamesListsAllFour()
    {
        Assert.Equal(new[] { "dark", "light", "claude", "mono" }, Presets.Names);
    }
}
