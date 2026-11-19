using System.ComponentModel;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ScreentimeTracker.Core;
using Ui = Wpf.Ui.Controls;

namespace ScreentimeTracker.App;

/// <summary>
/// The Interruption: an always-on-top window, centred and not fullscreen, listing every ringing
/// Timer, each with big Snooze and Dismiss buttons and a Start another, plus a New Timer starter. It plays a sound and flashes the taskbar each time
/// another Timer starts ringing, and stays until no Timer is ringing; its close button does nothing.
/// The Core shows, updates and hides it as the Interruption presenter.
/// </summary>
public partial class InterruptionWindow : Ui.FluentWindow, IInterruptionPresenter
{
    private const uint FlashAll = 0x3; // FLASHW_ALL
    private const uint FlashUntilForeground = 0xC; // FLASHW_TIMERNOFG

    private bool _quitting;

    public InterruptionWindow()
    {
        InitializeComponent();
        ThemeFollower.Follow(this);
        // The height follows the ringing Timers; grow and shrink about the middle so it stays centred.
        SizeChanged += (_, e) =>
        {
            if (IsLoaded && e.PreviousSize.Height > 0 && e.HeightChanged)
                Top -= (e.NewSize.Height - e.PreviousSize.Height) / 2;
        };
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

    /// <summary>Lists the ringing Timers, one card each.</summary>
    private void List(IReadOnlyList<TimerSnapshot> ringing)
    {
        RingingList.Children.Clear();
        var snooze = $"Snooze {ScreenTimeTracker.SnoozeLength.TotalMinutes:0} minutes";
        foreach (var timer in ringing)
        {
            var name = new TextBlock
            {
                Text = timer.Name,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 14),
            };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

            var buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var snoozeButton = BigButton(snooze, Ui.ControlAppearance.Primary, () => SnoozeRequested?.Invoke(timer.Id));
            var dismissButton = BigButton("Dismiss", Ui.ControlAppearance.Secondary, () => DismissRequested?.Invoke(timer.Id));
            Grid.SetColumn(dismissButton, 2);
            buttons.Children.Add(snoozeButton);
            buttons.Children.Add(dismissButton);

            var startAnother = new Ui.Button
            {
                Content = "Start another",
                Appearance = Ui.ControlAppearance.Transparent,
                Icon = new Ui.SymbolIcon(Ui.SymbolRegular.ArrowRepeatAll24),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0),
            };
            startAnother.Click += (_, _) => StartAnotherRequested?.Invoke(timer.Id);

            var content = new StackPanel();
            content.Children.Add(name);
            content.Children.Add(buttons);
            content.Children.Add(startAnother);
            var card = new Border { Padding = new Thickness(20, 16, 20, 12), Margin = new Thickness(0, 0, 0, 10), Child = content };
            card.SetResourceReference(StyleProperty, "Card");
            RingingList.Children.Add(card);
        }
    }

    private static Ui.Button BigButton(string text, Ui.ControlAppearance appearance, Action onClick)
    {
        var button = new Ui.Button
        {
            Content = text,
            Appearance = appearance,
            FontSize = 16,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
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
