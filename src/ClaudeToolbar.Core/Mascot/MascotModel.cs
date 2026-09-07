using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Mascot;

/// <summary>Session badge shown on Clawd's shoulder, ordered by strength.</summary>
public enum MascotBadge { None, Working, Finished, Failed, Attention }

public static class MascotBadgeColors
{
    public static string? Hex(MascotBadge badge) => badge switch
    {
        MascotBadge.Working => "#FF3B82F6",
        MascotBadge.Finished => "#FF3FB950",
        MascotBadge.Failed => "#FFF85149",
        MascotBadge.Attention => "#FFD29922",
        _ => null,
    };
}

public sealed record MascotModel(bool Visible, double ArmAngle, bool Dimmed, MascotBadge Badge, bool BadgeLit)
{
    public static readonly MascotModel Hidden = new(false, WaveAnimation.RestAngle, false, MascotBadge.None, false);
}

public static class MascotModelBuilder
{
    public static MascotModel Build(WidgetModel widget, string mascotMode, double armAngle, MascotBadge badge = MascotBadge.None, bool badgeLit = true)
    {
        if (MascotMode.Normalize(mascotMode) == MascotMode.Off || widget.Rows.Count == 0)
            return MascotModel.Hidden;
        return new MascotModel(true, armAngle, widget.Dimmed, badge, badgeLit);
    }
}
