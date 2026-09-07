namespace ClaudeToolbar.Core.Mascot;

/// <summary>Why the mascot should wave. Ordered by strength so the strongest cue in a batch wins.</summary>
public enum MascotCue
{
    Greeting,
    Hover,
    Warn,
    Crit,
    Finished,
    Failed,
    Attention,
}
