namespace ClaudeToolbar.Core.Mascot;

public enum ClawdPose { Rest, Mid, Up, High }

public enum CellKind { Body, Eye }

public readonly record struct SpriteCell(int Col, int Row, CellKind Kind);

/// <summary>Clawd as data: an 18×6 grid of w×2w cells decoded from the Claude Code CLI's block sprite. Row 0 is only used by the raised arm.</summary>
public static class ClawdSprite
{
    public const int Columns = 18;
    public const int Rows = 6;
    public const string BodyColor = "#FFD97757";
    public const string EyeColor = "#FF1B1B1B";
    public const double BadgeCenterCol = 16.5;
    public const double BadgeCenterRow = 0.6;
    public const double BadgeRadiusCells = 2;

    public static ClawdPose PoseFor(double armAngle) =>
        armAngle <= -30 ? ClawdPose.Rest :
        armAngle <= 10 ? ClawdPose.Mid :
        armAngle <= 40 ? ClawdPose.Up : ClawdPose.High;

    public static IReadOnlyList<SpriteCell> Cells(ClawdPose pose)
    {
        var cells = new List<SpriteCell>(56);
        for (var c = 3; c <= 14; c++) cells.Add(new SpriteCell(c, 1, CellKind.Body));
        for (var c = 3; c <= 14; c++) cells.Add(new SpriteCell(c, 2, c is 5 or 12 ? CellKind.Eye : CellKind.Body));
        for (var c = 1; c <= 14; c++) cells.Add(new SpriteCell(c, 3, CellKind.Body));
        for (var c = 3; c <= 14; c++) cells.Add(new SpriteCell(c, 4, CellKind.Body));
        foreach (var c in new[] { 4, 6, 11, 13 }) cells.Add(new SpriteCell(c, 5, CellKind.Body));

        var (inner, outer) = pose switch
        {
            ClawdPose.Rest => (3, 3),
            ClawdPose.Mid => (3, 2),
            ClawdPose.Up => (2, 1),
            _ => (1, 0),
        };
        cells.Add(new SpriteCell(15, inner, CellKind.Body));
        cells.Add(new SpriteCell(16, outer, CellKind.Body));
        return cells;
    }
}
