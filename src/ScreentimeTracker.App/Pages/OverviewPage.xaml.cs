using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreentimeTracker.App.Charts;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App.Pages;

/// <summary>
/// Screen Time for either Today or the Last 7 Days: Today as a donut split by Category, the Last 7 Days
/// as a stacked column per Day, each with a legend of Category totals. Below, the period's Sources (Apps
/// and Websites) by Screen Time, most first, each with its Category, which can be changed inline.
/// Refreshes while it is visible.
/// </summary>
public partial class OverviewPage : Page
{
    private readonly ScreenTimeTracker _tracker;
    private readonly DispatcherTimer _refresher;
    private readonly Dictionary<Category, TextBlock> _todayLegend = [];
    private readonly Dictionary<Category, TextBlock> _weekLegend = [];
    private readonly TextBlock _weekTotal = new() { FontSize = 14, FontWeight = FontWeights.SemiBold };
    private bool _choosingCategory;

    public OverviewPage(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        BuildLegends();
        _refresher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refresher.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { Refresh(); _refresher.Start(); }
            else _refresher.Stop();
        };
    }

    private void BuildLegends()
    {
        var overall = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 36, 0) };
        overall.Children.Add(Text("7-Day total", 14, FontWeights.Normal, "TextFillColorSecondaryBrush", margin: 6));
        _weekTotal.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        overall.Children.Add(_weekTotal);
        WeekLegend.Children.Add(overall);

        foreach (var category in CategoryColors.ChartOrder)
        {
            // Today: one row per Category, such as "Productive   2h 10m · 52%".
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            row.Children.Add(Swatch(category, 14));
            row.Children.Add(Text(category.ToString(), 16, FontWeights.SemiBold, "TextFillColorPrimaryBrush", minWidth: 130));
            var figures = Text("", 16, FontWeights.Normal, "TextFillColorSecondaryBrush");
            row.Children.Add(figures);
            TodayLegend.Children.Add(row);
            _todayLegend[category] = figures;

            // Last 7 Days: each Category's total, after the overall total.
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 28, 0) };
            item.Children.Add(Swatch(category, 12));
            item.Children.Add(Text(category.ToString(), 14, FontWeights.Normal, "TextFillColorSecondaryBrush", margin: 6));
            var total = Text("", 14, FontWeights.SemiBold, "TextFillColorPrimaryBrush");
            item.Children.Add(total);
            WeekLegend.Children.Add(item);
            _weekLegend[category] = total;
        }
    }

    private static Ellipse Swatch(Category category, double size)
    {
        var swatch = new Ellipse { Width = size, Height = size, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        swatch.SetResourceReference(Shape.FillProperty, CategoryColors.BrushKey(category));
        return swatch;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, string brushKey,
        double minWidth = 0, double margin = 0)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            MinWidth = minWidth,
            Margin = new Thickness(0, 0, margin, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }

    private void Period_Checked(object sender, RoutedEventArgs e)
    {
        // Checked fires for the default option while the page is still being built.
        if (_tracker is not null) Refresh();
    }

    // Refreshing replaces the rows, which would close a Category drop-down the user has open.
    private void CategoryDropDown_Opened(object? sender, EventArgs e) => _choosingCategory = true;

    private void CategoryDropDown_Closed(object? sender, EventArgs e) => _choosingCategory = false;

    private void Refresh()
    {
        if (_choosingCategory) return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var showingWeek = Last7DaysOption.IsChecked == true;
        var report = showingWeek ? _tracker.Last7Days(today) : _tracker.ReportFor(today, today);

        TodayPanel.Visibility = showingWeek ? Visibility.Collapsed : Visibility.Visible;
        WeekPanel.Visibility = showingWeek ? Visibility.Visible : Visibility.Collapsed;
        if (showingWeek) ShowWeek(report, today);
        else ShowToday(report);

        SourceList.ItemsSource = report.Sources.Select(s => new SourceRow(s, Recategorize)).ToList();
        NoSourcesText.Visibility = report.Sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowToday(PeriodReport report)
    {
        Donut.Show(report, "Today");
        var total = report.ScreenTime;
        foreach (var category in CategoryColors.ChartOrder)
        {
            var time = report.For(category);
            _todayLegend[category].Text = $"{TimeFormat.Short(time)} · {TimeFormat.Share(time, total)}";
        }
    }

    private void ShowWeek(PeriodReport report, DateOnly today)
    {
        Week.Show(report.Days, today);
        _weekTotal.Text = TimeFormat.Short(report.ScreenTime);
        foreach (var category in CategoryColors.ChartOrder)
        {
            _weekLegend[category].Text = TimeFormat.Short(report.For(category));
        }
    }

    private void Recategorize(SourceTime source, Category category)
    {
        _tracker.SetCategory(source.Kind, source.Key, category);
        // Let the ComboBox finish its own selection change before its row is replaced.
        Dispatcher.BeginInvoke(Refresh, DispatcherPriority.Background);
    }

    /// <summary>A row in the Source list. Choosing a Category in the row re-files the Source.</summary>
    private sealed class SourceRow(SourceTime source, Action<SourceTime, Category> recategorize)
    {
        private Category _category = source.Category;

        public string Name => source.DisplayName;
        public string Time => TimeFormat.Long(source.ScreenTime);

        public Category Category
        {
            get => _category;
            set
            {
                if (value == _category) return;
                _category = value;
                recategorize(source, value);
            }
        }
    }
}
