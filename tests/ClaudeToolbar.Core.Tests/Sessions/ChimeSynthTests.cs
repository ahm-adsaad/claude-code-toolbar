using System.Text;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.Core.Tests.Sessions;

public class ChimeSynthTests
{
    [Theory]
    [InlineData(ChimeKind.Attention)]
    [InlineData(ChimeKind.Finished)]
    [InlineData(ChimeKind.Failed)]
    public void WavHeaderMatchesTheSamples(ChimeKind kind)
    {
        var samples = ChimeSynth.Samples(kind);
        var wav = ChimeSynth.Wav(kind);
        Assert.Equal(44 + samples.Length * 2, wav.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));
        Assert.Equal(ChimeSynth.SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(wav, 40));
        Assert.All(samples, s => Assert.InRange(Math.Abs(s), 0, ChimeSynth.Amplitude + 1e-9));
        Assert.Contains(samples, s => Math.Abs(s) > ChimeSynth.Amplitude * 0.9);
    }

    [Fact]
    public void DurationsFollowTheNotes()
    {
        Assert.Equal(2, ChimeSynth.Notes(ChimeKind.Attention).Count);
        Assert.Single(ChimeSynth.Notes(ChimeKind.Finished));
        Assert.Equal(2, ChimeSynth.Notes(ChimeKind.Failed).Count);
        var finished = ChimeSynth.Samples(ChimeKind.Finished);
        Assert.Equal((int)((0.25 + 0.02) * ChimeSynth.SampleRate), finished.Length);
        Assert.Equal(0, finished[0], 6);
        Assert.Equal(0, finished[^1], 3);
    }
}
