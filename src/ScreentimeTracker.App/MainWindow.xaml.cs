using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>
/// Shows today's total Screen Time and its split by Category, and lists today's Sources (Apps and
/// Websites) by Screen Time, most first, each with its Category, which can be changed inline.
/// Refreshes while it is visible.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ScreenTimeTracker _tracker;
    private readonly DispatcherTimer _refresher;
    private bool _quitting;
    private bool _choosingCategory;

    public MainWindow(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        _refresher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refresher.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { Refresh(); ShowIdleThreshold(); _refresher.Start(); }
            else _refresher.Stop();
        };
    }

    private void ShowIdleThreshold()
    {
        IdleThresholdBox.Text = _tracker.IdleThreshold.TotalMinutes.ToString("0.##", CultureInfo.CurrentCulture);
        IdleThresholdStatus.Text = "";
    }

    private void ApplyIdleThreshold_Click(object sender, RoutedEventArgs e) => ApplyIdleThreshold();

    private void IdleThresholdBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyIdleThreshold();
    }

    private void ApplyIdleThreshold()
    {
        var min = ScreenTimeTracker.MinIdleThreshold.TotalMinutes;
        var max = ScreenTimeTracker.MaxIdleThreshold.TotalMinutes;
        if (!double.TryParse(IdleThresholdBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var minutes)
            || minutes < min || minutes > max)
        {
            IdleThresholdStatus.Text = $"Enter minutes from {min:0.##} to {max:0.##}";
            return;
        }

        _tracker.SetIdleThreshold(TimeSpan.FromMinutes(minutes));
        ShowIdleThreshold();
        IdleThresholdStatus.Text = "Saved";
    }

    public void CloseForQuit()
    {
        _quitting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_quitting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    // Refreshing replaces the rows, which would close a Category drop-down the user has open.
    private void CategoryDropDown_Opened(object? sender, EventArgs e) => _choosingCategory = true;

    private void CategoryDropDown_Closed(object? sender, EventArgs e) => _choosingCategory = false;

    private void Refresh()
    {
        if (_choosingCategory) return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var sources = _tracker.SourcesOn(today);
        SourceList.ItemsSource = sources.Select(s => new SourceRow(s, Recategorize)).ToList();

        var totals = _tracker.TotalsOn(today);
        TotalText.Text = Format(totals.ScreenTime);
        ProductiveText.Text = Format(totals.For(Category.Productive));
        EntertainmentText.Text = Format(totals.For(Category.Entertainment));
        UncategorizedText.Text = Format(totals.For(Category.Uncategorized));
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
        public string Time => Format(source.ScreenTime);

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

    private static string Format(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}h {time.Minutes:00}m"
        : $"{time.Minutes}m {time.Seconds:00}s";
}
