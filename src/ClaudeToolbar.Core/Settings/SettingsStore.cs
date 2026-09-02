using System.Text.Json;

namespace ClaudeToolbar.Core.Settings;

public sealed class SettingsStore
{
    public SettingsStore(string path) => Path = path;

    public string Path { get; }

    public static string DefaultPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeToolbar", "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(Path))
            return SettingsValidator.Normalize(AppSettings.CreateDefault());

        try
        {
            var json = File.ReadAllText(Path);
            return SettingsValidator.Normalize(SettingsJson.Deserialize(json));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackupBad();
            var defaults = SettingsValidator.Normalize(AppSettings.CreateDefault());
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, SettingsJson.Serialize(SettingsValidator.Normalize(settings)));
        File.Move(tmp, Path, overwrite: true);
    }

    private void TryBackupBad()
    {
        try
        {
            File.Copy(Path, Path + ".bad", overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
