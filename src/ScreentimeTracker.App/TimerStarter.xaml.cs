using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreentimeTracker.App;

/// <summary>Takes an optional label, picks or types a Timer duration, and asks for that Timer.</summary>
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
                MinWidth = 52,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 6, 6),
            };
            button.Click += (_, _) => Request(preset);
            PresetButtons.Children.Add(button);
        }
    }

    /// <summary>Raised with the chosen duration and the label as typed, when the user starts a Timer.</summary>
    public event Action<TimeSpan, string?>? StartRequested;

    /// <summary>Clears the label, typed minutes and any error, as when the control is shown afresh.</summary>
    public void Clear()
    {
        LabelBox.Text = "";
        MinutesBox.Text = "";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void Start_Click(object sender, RoutedEventArgs e) => StartTyped();

    private void TypedBox_KeyDown(object sender, KeyEventArgs e)
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
        // The Core treats a blank label as none.
        var label = LabelBox.Text;
        Clear();
        StartRequested?.Invoke(duration, label);
    }
}
