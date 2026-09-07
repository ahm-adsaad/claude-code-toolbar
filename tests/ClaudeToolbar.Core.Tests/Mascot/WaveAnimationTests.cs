using ClaudeToolbar.Core.Mascot;

namespace ClaudeToolbar.Core.Tests.Mascot;

public class WaveAnimationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void RestsOutsideTheWave(double progress) => Assert.Equal(WaveAnimation.RestAngle, WaveAnimation.ArmAngle(progress));

    [Fact]
    public void RaisedAtTheMidpoint() => Assert.Equal(WaveAnimation.RaisedAngle, WaveAnimation.ArmAngle(0.5), 6);

    [Fact]
    public void RampRisesMonotonically()
    {
        var previous = WaveAnimation.ArmAngle(0);
        for (var p = 0.01; p <= WaveAnimation.RampFraction + 1e-9; p += 0.01)
        {
            var angle = WaveAnimation.ArmAngle(p);
            Assert.True(angle > previous, $"angle at {p} should exceed {previous}");
            previous = angle;
        }
        Assert.Equal(WaveAnimation.RaisedAngle, WaveAnimation.ArmAngle(WaveAnimation.RampFraction), 6);
    }

    [Fact]
    public void RampsAreSymmetric() => Assert.Equal(WaveAnimation.ArmAngle(0.075), WaveAnimation.ArmAngle(0.925), 9);

    [Fact]
    public void WobbleStaysWithinBounds()
    {
        for (var p = WaveAnimation.RampFraction; p <= 1 - WaveAnimation.RampFraction; p += 0.005)
        {
            var angle = WaveAnimation.ArmAngle(p);
            Assert.InRange(angle, WaveAnimation.RaisedAngle - WaveAnimation.WobbleDegrees - 1e-9, WaveAnimation.RaisedAngle + WaveAnimation.WobbleDegrees + 1e-9);
        }
    }

    [Fact]
    public void ProgressClampsToTheDuration()
    {
        var start = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(0, WaveAnimation.Progress(start, start.AddSeconds(-1)));
        Assert.Equal(0.5, WaveAnimation.Progress(start, start.AddMilliseconds(600)), 9);
        Assert.Equal(1, WaveAnimation.Progress(start, start.AddSeconds(5)));
        Assert.Equal(TimeSpan.FromMilliseconds(1200), WaveAnimation.Duration);
        Assert.Equal(15, WaveAnimation.FramesPerSecond);
    }
}
