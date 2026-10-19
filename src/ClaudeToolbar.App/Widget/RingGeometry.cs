using System.Windows;
using System.Windows.Media;

namespace ClaudeToolbar.App.Widget;

/// <summary>The filled part of a usage ring, clockwise from twelve o'clock.</summary>
public static class RingGeometry
{
    /// <param name="diameter">The ring's outer size; a stroke of <paramref name="thickness"/> stays inside it.</param>
    public static Geometry Arc(double fraction, double diameter, double thickness)
    {
        if (double.IsNaN(fraction) || fraction <= 0) return Geometry.Empty;
        var c = diameter / 2;
        var r = (diameter - thickness) / 2;
        Geometry geometry;
        if (fraction >= 1)
        {
            geometry = new EllipseGeometry(new Point(c, c), r, r);
        }
        else
        {
            var angle = fraction * 2 * Math.PI;
            var end = new Point(c + r * Math.Sin(angle), c - r * Math.Cos(angle));
            var figure = new PathFigure { StartPoint = new Point(c, c - r), IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, isStroked: true));
            geometry = new PathGeometry { Figures = { figure } };
        }
        geometry.Freeze();
        return geometry;
    }
}
