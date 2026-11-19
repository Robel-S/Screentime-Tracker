using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreentimeTracker.App;

/// <summary>Picks or types a Timer duration and asks for a Timer of that length.</summary>
public partial class TimerStarter : UserControl
{
    public TimerStarter()
    {
        InitializeComponent();
        foreach (var preset in TimerDurations.Presets)
        {
            var button = new Button
            {
                Content = TimerDurations.ShortLabel(preset),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 4, 0),
            };
            button.Click += (_, _) => Request(preset);
            PresetButtons.Children.Add(button);
        }
    }

    /// <summary>Raised with the chosen duration when the user starts a Timer.</summary>
    public event Action<TimeSpan>? StartRequested;

    /// <summary>Clears typed minutes and any error, as when the control is shown afresh.</summary>
    public void Clear()
    {
        MinutesBox.Text = "";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void Start_Click(object sender, RoutedEventArgs e) => StartTyped();

    private void MinutesBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        // Enter belongs to the box, never to a button elsewhere in the window.
        e.Handled = true;
        StartTyped();
    }

    private void StartTyped()
    {
        if (TimerDurations.ParseMinutes(MinutesBox.Text) is { } duration)
        {
            Request(duration);
            return;
        }
        ErrorText.Text = TimerDurations.InvalidMinutesMessage;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Request(TimeSpan duration)
    {
        Clear();
        StartRequested?.Invoke(duration);
    }
}
