using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>Lists today's Apps by Screen Time, most first. Refreshes while it is visible.</summary>
public partial class MainWindow : Window
{
    private readonly ScreenTimeTracker _tracker;
    private readonly DispatcherTimer _refresher;
    private bool _quitting;

    public MainWindow(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        _refresher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refresher.Tick += (_, _) => Refresh();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { Refresh(); _refresher.Start(); }
            else _refresher.Stop();
        };
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

    private void Refresh()
    {
        SourceList.ItemsSource = _tracker.SourcesOn(DateOnly.FromDateTime(DateTime.Now))
            .Select(s => new { Name = s.DisplayName, Time = Format(s.ScreenTime) })
            .ToList();
    }

    private static string Format(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}h {time.Minutes:00}m"
        : $"{time.Minutes}m {time.Seconds:00}s";
}
