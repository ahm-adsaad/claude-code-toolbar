namespace ClaudeToolbar.Core.Credentials;

public static class CredentialsPaths
{
    public const string FileName = ".credentials.json";
    public const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";
    public const string SettingsFileName = "settings.json";

    public static string Resolve(string? claudeConfigDir, string userProfile)
    {
        var dir = string.IsNullOrWhiteSpace(claudeConfigDir)
            ? System.IO.Path.Combine(userProfile, ".claude")
            : claudeConfigDir.Trim();
        return System.IO.Path.Combine(dir, FileName);
    }

    public static string ResolveFromEnvironment() =>
        Resolve(Environment.GetEnvironmentVariable(ConfigDirVariable),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Claude Code's own settings file, next to the credentials file.</summary>
    public static string ClaudeSettingsPath(string? claudeConfigDir, string userProfile)
    {
        var dir = string.IsNullOrWhiteSpace(claudeConfigDir)
            ? Join(userProfile, ".claude")
            : claudeConfigDir.Trim();
        return Join(dir, SettingsFileName);
    }

    // Path.Combine always joins with the platform separator, so a forward-slash input on Windows
    // (e.g. "/Users/sam") would otherwise come back with a stray backslash mixed in.
    private static string Join(string dir, string leaf) =>
        dir.Contains('/')
            ? dir.TrimEnd('/', '\\') + "/" + leaf
            : System.IO.Path.Combine(dir, leaf);

    public static string ClaudeSettingsPathFromEnvironment() =>
        ClaudeSettingsPath(Environment.GetEnvironmentVariable(ConfigDirVariable),
                           Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
