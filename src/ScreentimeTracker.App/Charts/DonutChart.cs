using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App.Charts;

/// <summary>
/// A donut split by Category, each slice sized by its time and drawn clockwise from the top, with the
/// total in the hollow centre. Hovering a slice shows its figures. With no time it is an empty grey ring.
/// </summary>
internal sealed class DonutChart : Grid
{
    /// <summary>How much of the radius the hole takes up.</summary>
    private const double HoleRatio = 0.64;

    private readonly Canvas _canvas = new();
    private readonly Path _emptyRing = new();
    private readonly Dictionary<Category, (Path Slice, TextBlock Tip)> _slices = [];
    private readonly TextBlock _total = new() { FontSize = 30, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _caption = new() { FontSize = 14 };
    private PeriodReport? _report;

    public DonutChart()
    {
        _emptyRing.SetResourceReference(Shape.FillProperty, CategoryColors.EmptyBrushKey);
        _canvas.Children.Add(_emptyRing);
        foreach (var category in CategoryColors.ChartOrder)
        {
            var slice = new Path();
            var tip = CategoryShape.Prepare(slice, category);
            _canvas.Children.Add(slice);
            _slices[category] = (slice, tip);
        }
        Children.Add(_canvas);

        _total.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _caption.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var centre = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        _total.HorizontalAlignment = HorizontalAlignment.Center;
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
        centre.Children.Add(_total);
        centre.Children.Add(_caption);
        Children.Add(centre);

        SizeChanged += (_, _) => Draw();
    }

    /// <summary>Charts <paramref name="report"/>, with <paramref name="caption"/> under the total.</summary>
    public void Show(PeriodReport report, string caption)
    {
        _report = report;
        _total.Text = TimeFormat.Short(report.ScreenTime);
        _caption.Text = caption;
        Draw();
    }

    private void Draw()
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (_report is null || size <= 0) return;

        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        var outer = size / 2;
        var inner = outer * HoleRatio;
        var total = _report.ScreenTime;

        _emptyRing.Data = total > TimeSpan.Zero ? null : Ring(centre, outer, inner);
        var start = 0.0;
        foreach (var category in CategoryColors.ChartOrder)
        {
            var (slice, tip) = _slices[category];
            var time = _report.For(category);
            tip.Text = $"{category}\n{TimeFormat.Exact(time)} · {TimeFormat.Share(time, total)}";
            var sweep = total > TimeSpan.Zero ? time / total * 360 : 0;
            slice.Data = sweep <= 0 ? null
                : sweep >= 359.99 ? Ring(centre, outer, inner)
                : Sector(centre, outer, inner, start, start + sweep);
            start += sweep;
        }
    }

    private static Geometry Ring(Point centre, double outer, double inner) => new GeometryGroup
    {
        FillRule = FillRule.EvenOdd,
        Children =
        {
            new EllipseGeometry(centre, outer, outer),
            new EllipseGeometry(centre, inner, inner),
        },
    };

    /// <summary>The part of the ring between two angles, in degrees clockwise from the top.</summary>
    private static Geometry Sector(Point centre, double outer, double inner, double from, double to)
    {
        var large = to - from > 180;
        var figure = new PathFigure { StartPoint = At(centre, outer, from), IsClosed = true };
        figure.Segments.Add(new ArcSegment(At(centre, outer, to), new Size(outer, outer), 0, large,
            SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(At(centre, inner, to), true));
        figure.Segments.Add(new ArcSegment(At(centre, inner, from), new Size(inner, inner), 0, large,
            SweepDirection.Counterclockwise, true));
        return new PathGeometry { Figures = { figure } };
    }

    private static Point At(Point centre, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(centre.X + radius * Math.Sin(radians), centre.Y - radius * Math.Cos(radians));
    }
}
