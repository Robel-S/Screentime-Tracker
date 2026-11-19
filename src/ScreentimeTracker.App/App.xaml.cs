using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using ScreentimeTracker.Core;
using Forms = System.Windows.Forms;

namespace ScreentimeTracker.App;

/// <summary>
/// The tray host: owns the tracker and the local listener the browser extension reports to, ticks
/// the tracker about once a second, opens the main window from the tray icon, starts and cancels
/// the Timer from the tray menu, and hosts the Interruption window. Closing the main window hides it;
/// only Quit stops the app.
/// </summary>
public partial class App : Application
{
    private Win32SystemSignals? _signals;
    private LocalTabListener? _tabListener;
    private ScreenTimeTracker? _tracker;
    private DispatcherTimer? _ticker;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private InterruptionWindow? _interruption;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreentimeTracker");
        Directory.CreateDirectory(dataDirectory);

        var clock = new SystemClock();
        _signals = new Win32SystemSignals();
        var tabs = new ActiveTabFeed(clock);
        try
        {
            _tabListener = LocalTabListener.Start(tabs);
        }
        catch (SocketException)
        {
            // The port is taken (perhaps by another copy of the app). Browser time then goes to the
            // browser App, as it does whenever the extension can't report.
        }

        var interruption = new InterruptionWindow();
        _interruption = interruption;
        var tracker = ScreenTimeTracker.Open(
            Path.Combine(dataDirectory, "screentime.db"), _signals, tabs, interruption, clock);
        _tracker = tracker;
        interruption.SnoozeRequested += tracker.Snooze;
        interruption.DismissRequested += tracker.Dismiss;
        interruption.StartRequested += tracker.StartTimer;

        _ticker = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => { tracker.Tick(); ShowTimerInTray(); }, Dispatcher);
        _ticker.Start();
        tracker.Tick();

        _mainWindow = new MainWindow(tracker);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        var timerStatus = new Forms.ToolStripMenuItem { Enabled = false };
        menu.Items.Add(timerStatus);
        menu.Items.Add(TimerMenu(tracker));
        var cancelTimer = new Forms.ToolStripMenuItem("Cancel Timer", null, (_, _) => tracker.CancelTimer());
        menu.Items.Add(cancelTimer);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        menu.Opening += (_, _) =>
        {
            timerStatus.Text = TimerText.Describe(tracker);
            cancelTimer.Enabled = tracker.TimerState != TimerState.None;
        };

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Screentime Tracker",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) ShowMainWindow();
        };
    }

    /// <summary>The tray's "Start Timer" menu: preset durations, and a box to type minutes into.</summary>
    private Forms.ToolStripMenuItem TimerMenu(ScreenTimeTracker tracker)
    {
        const string typeHint = "Or type minutes, then press Enter:";
        var start = new Forms.ToolStripMenuItem("Start Timer");
        foreach (var preset in TimerDurations.Presets)
        {
            start.DropDownItems.Add(TimerDurations.LongLabel(preset), null, (_, _) => tracker.StartTimer(preset));
        }
        start.DropDownItems.Add(new Forms.ToolStripSeparator());
        var hint = new Forms.ToolStripLabel(typeHint);
        var typed = new Forms.ToolStripTextBox();
        typed.KeyDown += (_, args) =>
        {
            if (args.KeyCode != Forms.Keys.Enter) return;
            args.SuppressKeyPress = true;
            if (TimerDurations.ParseMinutes(typed.Text) is not { } duration)
            {
                hint.Text = TimerDurations.InvalidMinutesMessage;
                typed.SelectAll();
                return;
            }
            tracker.StartTimer(duration);
            typed.Text = "";
            hint.Text = typeHint;
            _trayIcon?.ContextMenuStrip?.Close();
        };
        start.DropDownItems.Add(hint);
        start.DropDownItems.Add(typed);
        return start;
    }

    private void ShowTimerInTray()
    {
        if (_trayIcon is null || _tracker is null) return;
        _trayIcon.Text = _tracker.TimerState == TimerState.None
            ? "Screentime Tracker"
            : $"Screentime Tracker - {TimerText.Describe(_tracker)}";
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void Quit()
    {
        _mainWindow?.CloseForQuit();
        _interruption?.CloseForQuit();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ticker?.Stop();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _tabListener?.Dispose();
        _tracker?.Dispose();
        _signals?.Dispose();
        base.OnExit(e);
    }
}
