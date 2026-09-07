using ClaudeToolbar.Core.Mascot;

namespace ClaudeToolbar.Core.Tests.Mascot;

public class ClawdSpriteTests
{
    [Theory]
    [InlineData(-60, ClawdPose.Rest)]
    [InlineData(-30, ClawdPose.Rest)]
    [InlineData(-29.9, ClawdPose.Mid)]
    [InlineData(10, ClawdPose.Mid)]
    [InlineData(10.1, ClawdPose.Up)]
    [InlineData(40, ClawdPose.Up)]
    [InlineData(40.1, ClawdPose.High)]
    [InlineData(75, ClawdPose.High)]
    public void PoseFollowsTheArmAngle(double angle, ClawdPose expected) => Assert.Equal(expected, ClawdSprite.PoseFor(angle));

    [Fact]
    public void RestSpriteHasTheDecodedShape()
    {
        var cells = ClawdSprite.Cells(ClawdPose.Rest);
        Assert.Equal(56, cells.Count);
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, c => Assert.InRange(c.Col, 0, ClawdSprite.Columns - 1));
        Assert.All(cells, c => Assert.InRange(c.Row, 0, ClawdSprite.Rows - 1));
        Assert.Equal([new SpriteCell(5, 2, CellKind.Eye), new SpriteCell(12, 2, CellKind.Eye)], cells.Where(c => c.Kind == CellKind.Eye).OrderBy(c => c.Col).ToList());
        Assert.Contains(new SpriteCell(1, 3, CellKind.Body), cells);
        Assert.Contains(new SpriteCell(2, 3, CellKind.Body), cells);
        Assert.Equal([4, 6, 11, 13], cells.Where(c => c.Row == 5).Select(c => c.Col).OrderBy(c => c).ToList());
        Assert.DoesNotContain(cells, c => c.Row == 0);
        Assert.Equal(12, cells.Count(c => c.Row == 1));
    }

    [Theory]
    [InlineData(ClawdPose.Rest, 15, 3, 16, 3)]
    [InlineData(ClawdPose.Mid, 15, 3, 16, 2)]
    [InlineData(ClawdPose.Up, 15, 2, 16, 1)]
    [InlineData(ClawdPose.High, 15, 1, 16, 0)]
    public void RightArmMovesWithThePose(ClawdPose pose, int c1, int r1, int c2, int r2)
    {
        var arm = ClawdSprite.Cells(pose).Where(c => c.Col >= 15).OrderBy(c => c.Col).ToList();
        Assert.Equal([new SpriteCell(c1, r1, CellKind.Body), new SpriteCell(c2, r2, CellKind.Body)], arm);
        Assert.Equal(56, ClawdSprite.Cells(pose).Count);
    }

    [Fact]
    public void ColoursAndBadgeGeometryAreFixed()
    {
        Assert.Equal("#FFD97757", ClawdSprite.BodyColor);
        Assert.Equal("#FF1B1B1B", ClawdSprite.EyeColor);
        Assert.Equal(16.5, ClawdSprite.BadgeCenterCol);
        Assert.Equal(0.6, ClawdSprite.BadgeCenterRow);
        Assert.Equal(2, ClawdSprite.BadgeRadiusCells);
        Assert.Null(MascotBadgeColors.Hex(MascotBadge.None));
        Assert.Equal("#FFD29922", MascotBadgeColors.Hex(MascotBadge.Attention));
        Assert.Equal("#FF3B82F6", MascotBadgeColors.Hex(MascotBadge.Working));
        Assert.Equal("#FF3FB950", MascotBadgeColors.Hex(MascotBadge.Finished));
        Assert.Equal("#FFF85149", MascotBadgeColors.Hex(MascotBadge.Failed));
    }
}
