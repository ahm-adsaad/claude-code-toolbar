using System.Text.Json;

namespace ClaudeToolbar.Core.Pace;

/// <summary>Keeps the learned pace across restarts. A file it cannot use is ignored: the pace is learned again.</summary>
public sealed class PaceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public PaceStore(string path) => Path = path;

    public string Path { get; }

    public static string DefaultPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeToolbar", "pace.json");

    public PaceState? Load()
    {
        if (!File.Exists(Path)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<PaceState>(File.ReadAllText(Path), Options);
            if (state is null || state.Version != 1) return null;
            // A hand-edited file may hold nulls where the tracker expects lists.
            var limits = state.Limits ?? new();
            state.Limits = limits.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv =>
            {
                kv.Value.Points = (kv.Value.Points ?? new()).Where(p => p is not null).ToList();
                return kv.Value;
            });
            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(PaceState state)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Options));
        File.Move(tmp, Path, overwrite: true);
    }
}
