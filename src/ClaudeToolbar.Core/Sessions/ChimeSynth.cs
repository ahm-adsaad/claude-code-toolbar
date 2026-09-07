namespace ClaudeToolbar.Core.Sessions;

public enum ChimeKind { Attention, Finished, Failed }

/// <summary>Generates the notification chimes as 16-bit mono WAV data so no audio assets ship with the app.</summary>
public static class ChimeSynth
{
    public const int SampleRate = 44_100;
    public const double Amplitude = 0.3;
    private const double AttackSeconds = 0.008;
    private const double GapSeconds = 0.02;

    public static IReadOnlyList<(double Hz, double Seconds)> Notes(ChimeKind kind) => kind switch
    {
        ChimeKind.Attention => [(659.25, 0.12), (880.0, 0.20)],
        ChimeKind.Finished => [(523.25, 0.25)],
        _ => [(392.0, 0.14), (329.63, 0.22)],
    };

    public static double[] Samples(ChimeKind kind)
    {
        var notes = Notes(kind);
        var total = (int)(notes.Sum(n => n.Seconds + GapSeconds) * SampleRate);
        var buffer = new double[total];
        var offset = 0;
        foreach (var (hz, seconds) in notes)
        {
            var count = (int)(seconds * SampleRate);
            var attack = (int)(AttackSeconds * SampleRate);
            var fadeStart = (int)(count * 0.4);
            for (var i = 0; i < count && offset + i < buffer.Length; i++)
            {
                var envelope = i < attack ? i / (double)attack
                    : i > fadeStart ? 1 - (i - fadeStart) / (double)(count - fadeStart)
                    : 1;
                buffer[offset + i] = Amplitude * envelope * Math.Sin(2 * Math.PI * hz * i / SampleRate);
            }
            offset += count + (int)(GapSeconds * SampleRate);
        }
        return buffer;
    }

    public static byte[] Wav(ChimeKind kind)
    {
        var samples = Samples(kind);
        var dataBytes = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (var sample in samples)
            writer.Write((short)Math.Round(sample * short.MaxValue));
        writer.Flush();
        return stream.ToArray();
    }
}
