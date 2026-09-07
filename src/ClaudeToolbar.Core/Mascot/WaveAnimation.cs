namespace ClaudeToolbar.Core.Mascot;

/// <summary>Timing of one wave. Pure functions so both the widget and its tests agree on every frame.</summary>
public static class WaveAnimation
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(1200);
    public const int FramesPerSecond = 15;
    public const double RestAngle = -60;
    public const double RaisedAngle = 50;
    public const double WobbleDegrees = 25;
    public const int Wobbles = 3;
    public const double RampFraction = 0.15;

    /// <summary>Arm angle in degrees above horizontal for a wave at <paramref name="progress"/> (0…1).</summary>
    public static double ArmAngle(double progress)
    {
        if (progress <= 0 || progress >= 1) return RestAngle;

        double envelope;
        var wobble = 0.0;
        if (progress < RampFraction)
        {
            envelope = progress / RampFraction;
        }
        else if (progress > 1 - RampFraction)
        {
            envelope = (1 - progress) / RampFraction;
        }
        else
        {
            envelope = 1;
            var middle = (progress - RampFraction) / (1 - 2 * RampFraction);
            wobble = WobbleDegrees * Math.Sin(2 * Math.PI * Wobbles * middle);
        }

        return RestAngle + envelope * (RaisedAngle - RestAngle) + wobble;
    }

    public static double Progress(DateTimeOffset startedAt, DateTimeOffset now) =>
        Math.Clamp((now - startedAt) / Duration, 0, 1);
}
