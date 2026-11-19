using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>
/// Shows Screen Time for either Today or the Last 7 Days: the period's total and its split by Category,
/// each Day's totals when looking at the Last 7 Days, and the period's Sources (Apps and Websites) by
/// Screen Time, most first, each with its Category, which can be changed inline. Also starts, shows
/// and cancels the Timer. Refreshes while it is visible.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ScreenTimeTracker _tracker;
    private readonly DispatcherTimer _refresher;
    private readonly DispatcherTimer _timerDisplay;
    private bool _quitting;
    private bool _choosingCategory;

    public MainWindow(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        _refresher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refresher.Tick += (_, _) => Refresh();
        _timerDisplay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timerDisplay.Tick += (_, _) => ShowTimer();
        TimerStarter.StartRequested += duration =>
        {
            _tracker.StartTimer(duration);
            ShowTimer();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Refresh(); ShowIdleThreshold(); ShowTimer();
                ShowRunAtLogin();
                _refresher.Start(); _timerDisplay.Start();
            }
            else { _refresher.Stop(); _timerDisplay.Stop(); }
        };
    }

    private void ShowTimer()
    {
        TimerStatus.Text = TimerText.Describe(_tracker);
        CancelTimerButton.Visibility = _tracker.TimerState == TimerState.None ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CancelTimer_Click(object sender, RoutedEventArgs e)
    {
        _tracker.CancelTimer();
        ShowTimer();
    }

    private void ShowIdleThreshold()
    {
        IdleThresholdBox.Text = _tracker.IdleThreshold.TotalMinutes.ToString("0.##", CultureInfo.CurrentCulture);
        IdleThresholdStatus.Text = "";
    }

    private void ShowRunAtLogin()
    {
        RunAtLoginBox.IsChecked = RunAtLogin.IsEnabled;
        // A development build must not register itself, or stop the installed copy registering.
        RunAtLoginBox.IsEnabled = RunAtLogin.IsInstalledCopy;
        RunAtLoginBox.ToolTip = RunAtLogin.IsInstalledCopy ? null : "Only the installed copy (publish.ps1) can start at sign-in";
    }

    private void RunAtLoginBox_Click(object sender, RoutedEventArgs e)
    {
        RunAtLogin.Set(RunAtLoginBox.IsChecked == true);
        ShowRunAtLogin();
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

    private void Period_Checked(object sender, RoutedEventArgs e)
    {
        // Checked fires for the default option while the window is still being built.
        if (_tracker is not null) Refresh();
    }

    private void Refresh()
    {
        if (_choosingCategory) return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var showingWeek = Last7DaysOption.IsChecked == true;
        var report = showingWeek ? _tracker.Last7Days(today) : _tracker.ReportFor(today, today);

        PeriodText.Text = showingWeek ? "Last 7 Days" : "Today";
        SourceList.ItemsSource = report.Sources.Select(s => new SourceRow(s, Recategorize)).ToList();

        TotalText.Text = Format(report.ScreenTime);
        ProductiveText.Text = Format(report.For(Category.Productive));
        EntertainmentText.Text = Format(report.For(Category.Entertainment));
        UncategorizedText.Text = Format(report.For(Category.Uncategorized));

        DayList.Visibility = showingWeek ? Visibility.Visible : Visibility.Collapsed;
        // Most recent first, so Today sits at the top.
        DayList.ItemsSource = showingWeek ? report.Days.Reverse().Select(d => new DayRow(d, today)).ToList() : null;
    }

    /// <summary>A row in the Last 7 Days breakdown: one Day's total and Category totals.</summary>
    private sealed class DayRow(DayTotals totals, DateOnly today)
    {
        public string Day => totals.Day == today ? "Today"
            : totals.Day == today.AddDays(-1) ? "Yesterday"
            : totals.Day.ToString("ddd d MMM", CultureInfo.CurrentCulture);
        public string Total => Format(totals.ScreenTime);
        public string Productive => Format(totals.For(Category.Productive));
        public string Entertainment => Format(totals.For(Category.Entertainment));
        public string Uncategorized => Format(totals.For(Category.Uncategorized));
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
