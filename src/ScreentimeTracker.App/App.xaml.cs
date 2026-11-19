using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using ScreentimeTracker.Core;
using Wpf.Ui.Appearance;
using Forms = System.Windows.Forms;

namespace ScreentimeTracker.App;

/// <summary>
/// The tray host: owns the tracker and the local listener the browser extension reports to, ticks
/// the tracker about once a second, opens the main window from the tray icon, starts, lists and
/// cancels Timers from the tray menu, and hosts the Interruption window. Only one copy runs at a time; a
/// later launch opens the running copy's main window. Starts to the tray. Closing the main window
/// hides it; only Quit stops the app.
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
    private SingleInstance? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = SingleInstance.Claim();
        if (_singleInstance is null)
        {
            // Another copy is running and has been asked to show its main window.
            Shutdown();
            return;
        }
        _singleInstance.Listen(
            onShow: () => Dispatcher.BeginInvoke(ShowMainWindow),
            onQuit: () => Dispatcher.BeginInvoke(Quit));
        RunAtLogin.ApplyOnStartup();
        ApplicationThemeManager.ApplySystemTheme();
        CategoryColors.Follow(this);

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
        interruption.StartAnotherRequested += id => tracker.StartAnother(id);
        interruption.StartRequested += (duration, label) => tracker.StartTimer(duration, label);

        _ticker = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => { tracker.Tick(); ShowTimerInTray(); }, Dispatcher);
        _ticker.Start();
        tracker.Tick();

        _mainWindow = new MainWindow(tracker);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        var timersStart = new Forms.ToolStripSeparator();
        menu.Items.Add(timersStart);
        // The Timer items go between these two separators, rebuilt each time the menu opens.
        var timersEnd = new Forms.ToolStripSeparator();
        menu.Items.Add(timersEnd);
        menu.Items.Add(TimerMenu(tracker));
        var cancelAll = new Forms.ToolStripMenuItem("Cancel all Timers", null, (_, _) => tracker.CancelAllTimers());
        menu.Items.Add(cancelAll);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        menu.Opening += (_, _) =>
        {
            ListTimers(menu, timersStart, timersEnd, tracker);
            cancelAll.Enabled = tracker.Timers.Count > 0;
        };

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Screentime Tracker",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) ShowMainWindow();
        };
    }

    /// <summary>
    /// Lists every Timer in the tray menu, between <paramref name="timersStart"/> and
    /// <paramref name="timersEnd"/>: each with its time left and a Cancel item.
    /// </summary>
    private static void ListTimers(Forms.ContextMenuStrip menu, Forms.ToolStripItem timersStart,
        Forms.ToolStripItem timersEnd, ScreenTimeTracker tracker)
    {
        var first = menu.Items.IndexOf(timersStart) + 1;
        while (menu.Items.IndexOf(timersEnd) > first)
        {
            var old = menu.Items[first];
            menu.Items.RemoveAt(first);
            old.Dispose();
        }
        var timers = tracker.Timers;
        if (timers.Count == 0)
        {
            menu.Items.Insert(first, new Forms.ToolStripMenuItem(TimerText.NoTimers) { Enabled = false });
            return;
        }
        for (var i = 0; i < timers.Count; i++)
        {
            var id = timers[i].Id;
            // "&&", so an "&" in a label shows as itself rather than marking a shortcut key.
            var item = new Forms.ToolStripMenuItem(TimerText.Describe(timers[i]).Replace("&", "&&"));
            item.DropDownItems.Add("Cancel", null, (_, _) => tracker.CancelTimer(id));
            menu.Items.Insert(first + i, item);
        }
    }

    /// <summary>
    /// The tray's "Start Timer" menu: an optional label, then preset durations, and a box to type
    /// minutes into.
    /// </summary>
    private Forms.ToolStripMenuItem TimerMenu(ScreenTimeTracker tracker)
    {
        const string typeHint = "Or type minutes, then press Enter:";
        var start = new Forms.ToolStripMenuItem("Start Timer");
        var label = new Forms.ToolStripTextBox { ToolTipText = "Without a label, the Timer goes by its duration" };
        var hint = new Forms.ToolStripLabel(typeHint);
        var typed = new Forms.ToolStripTextBox();
        void Start(TimeSpan duration)
        {
            tracker.StartTimer(duration, label.Text);
            label.Text = "";
            typed.Text = "";
            hint.Text = typeHint;
        }

        start.DropDownItems.Add(new Forms.ToolStripLabel("Label (optional):"));
        start.DropDownItems.Add(label);
        start.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var preset in TimerDurations.Presets)
        {
            start.DropDownItems.Add(TimerDurations.LongLabel(preset), null, (_, _) => Start(preset));
        }
        start.DropDownItems.Add(new Forms.ToolStripSeparator());
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
            Start(duration);
            _trayIcon?.ContextMenuStrip?.Close();
        };
        start.DropDownItems.Add(hint);
        start.DropDownItems.Add(typed);
        return start;
    }

    private void ShowTimerInTray()
    {
        if (_trayIcon is null || _tracker is null) return;
        _trayIcon.Text = TimerText.Tooltip(_tracker.Timers);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream;
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
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
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
