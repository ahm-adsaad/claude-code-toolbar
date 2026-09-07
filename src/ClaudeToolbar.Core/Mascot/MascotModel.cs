using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Mascot;

public sealed record MascotModel(bool Visible, BarLevel Level, double ArmAngle, bool Dimmed)
{
    public static readonly MascotModel Hidden = new(false, BarLevel.Ok, WaveAnimation.RestAngle, false);
}

public static class MascotModelBuilder
{
    public static MascotModel Build(WidgetModel widget, string mascotMode, double armAngle)
    {
        if (MascotMode.Normalize(mascotMode) == MascotMode.Off || widget.Rows.Count == 0)
            return MascotModel.Hidden;

        var level = widget.Rows.Select(r => r.Level).Max();
        return new MascotModel(true, level, armAngle, widget.Dimmed);
    }
}
