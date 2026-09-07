using System.IO;
using System.Media;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.App.Services;

/// <summary>Writes the generated chimes to %LOCALAPPDATA%\ClaudeToolbar\sounds and plays them.</summary>
public sealed class ChimePlayer : IDisposable
{
    private readonly Dictionary<ChimeKind, SoundPlayer> _players = new();

    public ChimePlayer()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeToolbar", "sounds");
        Directory.CreateDirectory(dir);
        foreach (var kind in Enum.GetValues<ChimeKind>())
        {
            var path = Path.Combine(dir, kind.ToString().ToLowerInvariant() + ".wav");
            File.WriteAllBytes(path, ChimeSynth.Wav(kind));
            var player = new SoundPlayer(path);
            player.Load();
            _players[kind] = player;
        }
    }

    public void Play(ChimeKind kind)
    {
        try
        {
            _players[kind].Play();
        }
        catch (Exception ex)
        {
            Log.Error("Chime failed", ex);
        }
    }

    public void Dispose()
    {
        foreach (var player in _players.Values) player.Dispose();
        _players.Clear();
    }
}
