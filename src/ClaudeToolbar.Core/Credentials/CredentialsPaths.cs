namespace ClaudeToolbar.Core.Credentials;

public static class CredentialsPaths
{
    public const string FileName = ".credentials.json";
    public const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";

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
}
