using System.Windows;
using System.Windows.Media;
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Mascot;

namespace ClaudeToolbar.App.Widget;

/// <summary>The original code-drawn mascot. Geometry is the spec's 24-unit box, drawn 1:1 in logical pixels.</summary>
public sealed class MascotControl : FrameworkElement
{
    public const double Size = 24;

    private static readonly Brush Ink = Frozen(Color.FromArgb(0xD9, 0x1B, 0x1B, 0x1B));

    private MascotModel _model = MascotModel.Hidden;
    private WidgetTheme? _theme;

    public MascotControl()
    {
        Width = Size;
        Height = Size;
        IsHitTestVisible = false;
    }

    public void Update(MascotModel model, WidgetTheme theme)
    {
        var changed = model != _model || !ReferenceEquals(theme, _theme);
        _model = model;
        _theme = theme;
        Visibility = model.Visible ? Visibility.Visible : Visibility.Collapsed;
        if (changed) InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_model.Visible || _theme is null) return;
        var body = _theme.BrushFor(_model.Level);
        if (_model.Dimmed) dc.PushOpacity(0.5);

        // Arm first so the shoulder joint hides behind the body.
        var radians = _model.ArmAngle * Math.PI / 180;
        var shoulder = new Point(19, 12);
        var hand = new Point(19 + 6 * Math.Cos(radians), 12 - 6 * Math.Sin(radians));
        dc.DrawLine(RoundPen(body, 3), shoulder, hand);
        dc.DrawEllipse(body, null, hand, 2, 2);

        dc.DrawRoundedRectangle(body, null, new Rect(3, 5, 16, 16), 4, 4);
        dc.DrawEllipse(Ink, null, new Point(8.5, 11.5), 1.6, 1.6);
        dc.DrawEllipse(Ink, null, new Point(13.5, 11.5), 1.6, 1.6);

        var mouth = RoundPen(Ink, 1.4);
        switch (_model.Level)
        {
            case BarLevel.Ok:
                dc.DrawGeometry(null, mouth, Arc(new Point(11, 14.5), 3, 200, 340));
                break;
            case BarLevel.Warn:
                dc.DrawLine(mouth, new Point(8.5, 16), new Point(13.5, 16));
                break;
            default:
                dc.DrawGeometry(null, mouth, Arc(new Point(11, 18.5), 3, 20, 160));
                var brow = RoundPen(Ink, 1.2);
                dc.DrawLine(brow, new Point(7, 8.5), new Point(10, 9.5));
                dc.DrawLine(brow, new Point(15, 8.5), new Point(12, 9.5));
                break;
        }

        if (_model.Dimmed) dc.Pop();
    }

    /// <summary>Arc from <paramref name="fromDeg"/> to <paramref name="toDeg"/> (y-up angles, counter-clockwise on screen) on a y-down canvas.</summary>
    private static StreamGeometry Arc(Point center, double radius, double fromDeg, double toDeg)
    {
        Point At(double deg)
        {
            var r = deg * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(r), center.Y - radius * Math.Sin(r));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(fromDeg), false, false);
            ctx.ArcTo(At(toDeg), new Size(radius, radius), 0, toDeg - fromDeg > 180, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Pen RoundPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
