using System.ComponentModel;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>
/// The Interruption: an always-on-top window listing every ringing Timer, each with Snooze, Dismiss
/// and Start another, plus a New Timer starter. It plays a sound and flashes the taskbar each time
/// another Timer starts ringing, and stays until no Timer is ringing; its close button does nothing.
/// The Core shows, updates and hides it as the Interruption presenter.
/// </summary>
public partial class InterruptionWindow : Window, IInterruptionPresenter
{
    private const uint FlashAll = 0x3; // FLASHW_ALL
    private const uint FlashUntilForeground = 0xC; // FLASHW_TIMERNOFG

    private bool _quitting;

    public InterruptionWindow()
    {
        InitializeComponent();
        Starter.StartRequested += (duration, label) => StartRequested?.Invoke(duration, label);
    }

    /// <summary>Raised with the Timer when the user Snoozes it.</summary>
    public event Action<TimerId>? SnoozeRequested;

    /// <summary>Raised with the Timer when the user Dismisses it.</summary>
    public event Action<TimerId>? DismissRequested;

    /// <summary>Raised with the Timer when the user picks Start another on it.</summary>
    public event Action<TimerId>? StartAnotherRequested;

    /// <summary>Raised with the duration and label as typed when the user starts a New Timer.</summary>
    public event Action<TimeSpan, string?>? StartRequested;

    void IInterruptionPresenter.Ring(IReadOnlyList<TimerSnapshot> ringing)
    {
        // Keep what the user is typing if the window is already up.
        if (!IsVisible) Starter.Clear();
        List(ringing);
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        SystemSounds.Exclamation.Play();
        FlashTaskbar();
    }

    void IInterruptionPresenter.Update(IReadOnlyList<TimerSnapshot> ringing) => List(ringing);

    void IInterruptionPresenter.Hide() => Hide();

    /// <summary>Lists the ringing Timers, one row each.</summary>
    private void List(IReadOnlyList<TimerSnapshot> ringing)
    {
        RingingList.Children.Clear();
        RingingList.RowDefinitions.Clear();
        var snooze = $"Snooze {ScreenTimeTracker.SnoozeLength.TotalMinutes:0} minutes";
        foreach (var timer in ringing)
        {
            var row = RingingList.RowDefinitions.Count;
            RingingList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new TextBlock
            {
                Text = timer.Name,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 320,
                Margin = new Thickness(0, 4, 16, 4),
            };
            Place(name, row, 0);
            Place(RowButton(snooze, () => SnoozeRequested?.Invoke(timer.Id)), row, 1);
            Place(RowButton("Dismiss", () => DismissRequested?.Invoke(timer.Id)), row, 2);
            Place(RowButton("Start another", () => StartAnotherRequested?.Invoke(timer.Id)), row, 3);
        }
    }

    private void Place(UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        RingingList.Children.Add(element);
    }

    private static Button RowButton(string text, Action onClick)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(4) };
        button.Click += (_, _) => onClick();
        return button;
    }

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
