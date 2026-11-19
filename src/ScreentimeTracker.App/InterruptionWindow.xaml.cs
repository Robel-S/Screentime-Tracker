using System.ComponentModel;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>
/// The Interruption: an always-on-top window that plays a sound and flashes the taskbar when shown.
/// It stays until the user picks Snooze, Dismiss or a new Timer; its close button does nothing. The
/// Core shows and hides it as the Interruption presenter.
/// </summary>
public partial class InterruptionWindow : Window, IInterruptionPresenter
{
    private const uint FlashAll = 0x3; // FLASHW_ALL
    private const uint FlashUntilForeground = 0xC; // FLASHW_TIMERNOFG

    private bool _quitting;

    public InterruptionWindow()
    {
        InitializeComponent();
        SnoozeButton.Content = $"Snooze {ScreenTimeTracker.SnoozeLength.TotalMinutes:0} minutes";
        Starter.StartRequested += duration => StartRequested?.Invoke(duration);
    }

    /// <summary>Raised when the user picks Snooze.</summary>
    public event Action? SnoozeRequested;

    /// <summary>Raised when the user picks Dismiss.</summary>
    public event Action? DismissRequested;

    /// <summary>Raised with the duration when the user starts a new Timer from the Interruption.</summary>
    public event Action<TimeSpan>? StartRequested;

    void IInterruptionPresenter.Show()
    {
        Starter.Clear();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        SystemSounds.Exclamation.Play();
        FlashTaskbar();
    }

    void IInterruptionPresenter.Hide() => Hide();

    public void CloseForQuit()
    {
        _quitting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // The Interruption stays until the user acts on it.
        if (!_quitting) e.Cancel = true;
        base.OnClosing(e);
    }

    private void Snooze_Click(object sender, RoutedEventArgs e) => SnoozeRequested?.Invoke();

    private void Dismiss_Click(object sender, RoutedEventArgs e) => DismissRequested?.Invoke();

    /// <summary>
    /// Flashes the taskbar button until the window comes to the foreground, for when a fullscreen App
    /// keeps it from showing on top.
    /// </summary>
    private void FlashTaskbar()
    {
        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = new WindowInteropHelper(this).Handle,
            Flags = FlashAll | FlashUntilForeground,
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashInfo info);
}
