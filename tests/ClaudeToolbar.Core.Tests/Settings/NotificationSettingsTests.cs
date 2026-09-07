using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class NotificationSettingsTests
{
    [Fact]
    public void Defaults()
    {
        var n = AppSettings.CreateDefault().Notifications;
        Assert.True(n.Enabled);
        Assert.Equal(47831, n.Port);
        Assert.True(n.Sound);
    }

    [Fact]
    public void RoundTripAndMissingSection()
    {
        var s = AppSettings.CreateDefault();
        s.Notifications.Port = 50000;
        s.Notifications.Sound = false;
        var back = SettingsJson.Deserialize(SettingsJson.Serialize(s));
        Assert.Equal(50000, back.Notifications.Port);
        Assert.False(back.Notifications.Sound);
        Assert.Equal(47831, SettingsJson.Deserialize("{}").Notifications.Port);
    }

    [Fact]
    public void PortIsClamped()
    {
        var s = AppSettings.CreateDefault();
        s.Notifications.Port = 80;
        Assert.Equal(1024, SettingsValidator.Normalize(s).Notifications.Port);
        s.Notifications.Port = 70000;
        Assert.Equal(65535, SettingsValidator.Normalize(s).Notifications.Port);
    }

    [Fact]
    public void ClaudeSettingsPathFollowsTheConfigDir()
    {
        Assert.Equal("/Users/sam/.claude/settings.json", CredentialsPaths.ClaudeSettingsPath(null, "/Users/sam"));
        Assert.Equal("/tmp/cfg/settings.json", CredentialsPaths.ClaudeSettingsPath(" /tmp/cfg ", "/Users/sam"));
    }
}
