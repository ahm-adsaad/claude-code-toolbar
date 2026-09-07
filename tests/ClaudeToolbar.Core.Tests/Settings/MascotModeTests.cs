using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class MascotModeTests
{
    [Theory]
    [InlineData("full", "full")]
    [InlineData(" Hover ", "hover")]
    [InlineData("OFF", "off")]
    [InlineData("neon", "full")]
    [InlineData("", "full")]
    [InlineData(null, "full")]
    public void NormalizeAcceptsKnownModesAndFallsBackToFull(string? input, string expected) =>
        Assert.Equal(expected, MascotMode.Normalize(input));

    [Fact]
    public void DefaultIsFull() => Assert.Equal("full", AppSettings.CreateDefault().Behavior.Mascot);

    [Fact]
    public void RoundTripsThroughJson()
    {
        var s = AppSettings.CreateDefault();
        s.Behavior.Mascot = "off";
        var json = SettingsJson.Serialize(s);
        Assert.Contains("\"mascot\": \"off\"", json);
        Assert.Equal("off", SettingsJson.Deserialize(json).Behavior.Mascot);
    }

    [Fact]
    public void MissingKeyTakesDefaultAndValidatorNormalises()
    {
        Assert.Equal("full", SettingsJson.Deserialize("{ \"behavior\": {} }").Behavior.Mascot);
        var s = AppSettings.CreateDefault();
        s.Behavior.Mascot = "HOVER";
        Assert.Equal("hover", SettingsValidator.Normalize(s).Behavior.Mascot);
        s.Behavior.Mascot = "sparkles";
        Assert.Equal("full", SettingsValidator.Normalize(s).Behavior.Mascot);
    }
}
