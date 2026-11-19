using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using ScreentimeTracker.Core;
using Forms = System.Windows.Forms;

namespace ScreentimeTracker.App;

/// <summary>
/// The tray host: owns the tracker and the local listener the browser extension reports to, ticks
/// the tracker about once a second, and opens the main window from the tray icon. Closing the main window hides it; only Quit stops the app.
/// </summary>
public partial class App : Application
{
    private Win32SystemSignals? _signals;
    private LocalTabListener? _tabListener;
    private ScreenTimeTracker? _tracker;
    private DispatcherTimer? _ticker;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

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

        var tracker = ScreenTimeTracker.Open(
            Path.Combine(dataDirectory, "screentime.db"), _signals, tabs, clock);
        _tracker = tracker;

        _ticker = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => tracker.Tick(), Dispatcher);
        _ticker.Start();
        tracker.Tick();

        _mainWindow = new MainWindow(tracker);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Quit", null, (_, _) => Quit());

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
