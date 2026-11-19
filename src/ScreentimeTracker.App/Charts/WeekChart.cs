using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App.Charts;

/// <summary>
/// A stacked column per Day, oldest left and Today right and highlighted, each stacked bottom-up by
/// Category with its total above it, against a time axis scaled to the longest Day. Hovering a segment
/// shows that Category's exact time for the Day. A Day without Screen Time is an empty slot labelled "0m".
/// </summary>
internal sealed class WeekChart : Canvas
{
    private const double AxisWidth = 56;
    private const double DayLabelHeight = 30;
    private const double TotalLabelHeight = 26;
    private const double MaxColumnWidth = 64;

    /// <summary>The gridlines and their labels, redrawn whenever the scale or size changes.</summary>
    private readonly Canvas _grid = new() { IsHitTestVisible = false };
    private readonly Border _todayHighlight = new() { CornerRadius = new CornerRadius(8) };
    private readonly List<DayColumn> _columns = [];
    private IReadOnlyList<DayTotals> _days = [];
    private DateOnly _today;

    public WeekChart()
    {
        ClipToBounds = true;
        _todayHighlight.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        Children.Add(_todayHighlight);
        Children.Add(_grid);
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>Charts <paramref name="days"/>, oldest first, the last being <paramref name="today"/>.</summary>
    public void Show(IReadOnlyList<DayTotals> days, DateOnly today)
    {
        _days = days;
        _today = today;
        while (_columns.Count < days.Count) _columns.Add(new DayColumn(this));
        Draw();
    }

    private void Draw()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || _days.Count == 0) return;

        var plotLeft = AxisWidth;
        var plotTop = TotalLabelHeight;
        var plotBottom = ActualHeight - DayLabelHeight;
        var plotHeight = Math.Max(0, plotBottom - plotTop);
        var slot = (ActualWidth - plotLeft) / _days.Count;
        var columnWidth = Math.Min(slot * 0.55, MaxColumnWidth);

        var scale = ChartScale.Fit(_days.Max(d => d.ScreenTime));
        double Y(TimeSpan time) => plotBottom - plotHeight * (time / scale.Max);
        DrawGrid(scale, Y, plotLeft);

        for (var i = 0; i < _days.Count; i++)
        {
            var slotLeft = plotLeft + slot * i;
            var isToday = _days[i].Day == _today;
            if (isToday)
            {
                _todayHighlight.Width = slot - 4;
                _todayHighlight.Height = ActualHeight - 4;
                SetLeft(_todayHighlight, slotLeft + 2);
                SetTop(_todayHighlight, 2);
            }
            _columns[i].Draw(_days[i], DayLabel(_days[i].Day), isToday, slotLeft, slot, columnWidth, plotBottom, Y);
        }
        _todayHighlight.Visibility = _days.Any(d => d.Day == _today) ? Visibility.Visible : Visibility.Collapsed;
        // Columns that a shorter list of Days left over.
        for (var i = _days.Count; i < _columns.Count; i++) _columns[i].Hide();
    }

    private void DrawGrid(ChartScale scale, Func<TimeSpan, double> y, double plotLeft)
    {
        _grid.Children.Clear();
        for (var k = 0; k <= scale.Count; k++)
        {
            var time = scale.Step * k;
            var at = Math.Round(y(time)) + 0.5;
            var line = new Line { X1 = plotLeft, X2 = ActualWidth, Y1 = at, Y2 = at, StrokeThickness = 1 };
            line.SetResourceReference(Shape.StrokeProperty,
                k == 0 ? "ControlStrongStrokeColorDefaultBrush" : "DividerStrokeColorDefaultBrush");
            _grid.Children.Add(line);

            var label = new TextBlock
            {
                Text = TimeFormat.Short(time),
                Width = plotLeft - 10,
                TextAlignment = TextAlignment.Right,
                FontSize = 12,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            SetLeft(label, 0);
            SetTop(label, at - 8);
            _grid.Children.Add(label);
        }
    }

    private string DayLabel(DateOnly day) =>
        day == _today ? "Today" : day.ToString("ddd d", CultureInfo.CurrentCulture);

    /// <summary>One Day's slot: its stacked segments, the total above them and the Day below.</summary>
    private sealed class DayColumn
    {
        private readonly Dictionary<Category, (Rectangle Segment, TextBlock Tip)> _segments = [];
        private readonly Rectangle _emptySlot = new() { Height = 3, RadiusX = 1.5, RadiusY = 1.5 };
        private readonly TextBlock _total = new() { TextAlignment = TextAlignment.Center, FontSize = 13 };
        private readonly TextBlock _day = new() { TextAlignment = TextAlignment.Center, FontSize = 13 };

        public DayColumn(Canvas chart)
        {
            _emptySlot.SetResourceReference(Shape.FillProperty, CategoryColors.EmptyBrushKey);
            chart.Children.Add(_emptySlot);
            foreach (var category in CategoryColors.ChartOrder)
            {
                var segment = new Rectangle();
                var tip = CategoryShape.Prepare(segment, category);
                chart.Children.Add(segment);
                _segments[category] = (segment, tip);
            }
            _total.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            chart.Children.Add(_total);
            chart.Children.Add(_day);
        }

        public void Draw(DayTotals totals, string dayLabel, bool isToday, double slotLeft, double slotWidth,
            double columnWidth, double baseline, Func<TimeSpan, double> y)
        {
            var columnLeft = slotLeft + (slotWidth - columnWidth) / 2;
            var stacked = TimeSpan.Zero;
            foreach (var category in CategoryColors.ChartOrder)
            {
                var (segment, tip) = _segments[category];
                var time = totals.For(category);
                tip.Text = $"{category} · {dayLabel}\n{TimeFormat.Exact(time)}";
                var bottom = y(stacked);
                stacked += time;
                var top = y(stacked);
                segment.Visibility = time > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed;
                segment.Width = columnWidth;
                segment.Height = Math.Max(0, bottom - top);
                SetLeft(segment, columnLeft);
                SetTop(segment, top);
            }

            var empty = totals.ScreenTime <= TimeSpan.Zero;
            _emptySlot.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            _emptySlot.Width = columnWidth;
            SetLeft(_emptySlot, columnLeft);
            SetTop(_emptySlot, baseline - _emptySlot.Height);

            _total.Text = TimeFormat.Short(totals.ScreenTime);
            _total.Visibility = Visibility.Visible;
            _total.Width = slotWidth;
            SetLeft(_total, slotLeft);
            SetTop(_total, y(totals.ScreenTime) - (empty ? 24 : 22));

            _day.Text = dayLabel;
            _day.Visibility = Visibility.Visible;
            _day.FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal;
            _day.SetResourceReference(TextBlock.ForegroundProperty,
                isToday ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            _day.Width = slotWidth;
            SetLeft(_day, slotLeft);
            SetTop(_day, baseline + 7);
        }

        public void Hide()
        {
            foreach (var (segment, _) in _segments.Values) segment.Visibility = Visibility.Collapsed;
            _emptySlot.Visibility = Visibility.Collapsed;
            _total.Visibility = Visibility.Collapsed;
            _day.Visibility = Visibility.Collapsed;
        }
    }
}
