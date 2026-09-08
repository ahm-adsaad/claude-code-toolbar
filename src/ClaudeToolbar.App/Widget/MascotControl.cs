using System.Windows;
using System.Windows.Media;
using ClaudeToolbar.Core.Mascot;

namespace ClaudeToolbar.App.Widget;

/// <summary>Clawd, drawn cell by cell from <see cref="ClawdSprite"/>. Dimming comes from the parent rows control's opacity.</summary>
public sealed class MascotControl : FrameworkElement
{
    public const double Cell = 2;
    public static readonly double SpriteWidth = ClawdSprite.Columns * Cell;
    public static readonly double SpriteHeight = ClawdSprite.Rows * Cell * 2;

    private static readonly Brush Body = Frozen(ClawdSprite.BodyColor);
    private static readonly Brush Eye = Frozen(ClawdSprite.EyeColor);
    private static readonly Brush HitArea = Frozen("#01000000");
    private static readonly Dictionary<MascotBadge, Brush> BadgeBrushes = Enum.GetValues<MascotBadge>()
        .Where(b => MascotBadgeColors.Hex(b) is not null)
        .ToDictionary(b => b, b => (Brush)Frozen(MascotBadgeColors.Hex(b)!));

    private MascotModel _model = MascotModel.Hidden;

    public MascotControl()
    {
        Width = SpriteWidth;
        Height = SpriteHeight;
        IsHitTestVisible = true;
        SnapsToDevicePixels = true;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public void Update(MascotModel model)
    {
        if (model == _model) return;
        _model = model;
        Visibility = model.Visible ? Visibility.Visible : Visibility.Collapsed;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_model.Visible) return;
        // Alpha 1 of 255: invisible, but hit-testable across the whole sprite, gaps included.
        dc.DrawRectangle(HitArea, null, new Rect(0, 0, SpriteWidth, SpriteHeight));
        foreach (var cell in ClawdSprite.Cells(ClawdSprite.PoseFor(_model.ArmAngle)))
        {
            dc.DrawRectangle(cell.Kind == CellKind.Eye ? Eye : Body, null,
                new Rect(cell.Col * Cell, cell.Row * Cell * 2, Cell, Cell * 2));
        }

        if (_model.Badge != MascotBadge.None && _model.BadgeLit && BadgeBrushes.TryGetValue(_model.Badge, out var brush))
        {
            var center = new Point(ClawdSprite.BadgeCenterCol * Cell, ClawdSprite.BadgeCenterRow * Cell * 2);
            var radius = ClawdSprite.BadgeRadiusCells * Cell;
            dc.DrawEllipse(brush, null, center, radius, radius);
        }
    }

    private static SolidColorBrush Frozen(string argb)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(argb));
        brush.Freeze();
        return brush;
    }
}
