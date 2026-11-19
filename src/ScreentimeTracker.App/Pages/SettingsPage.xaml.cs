using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App.Pages;

/// <summary>The idle threshold, and whether the app starts when the user signs in.</summary>
public partial class SettingsPage : Page
{
    private const string RunAtLoginDescription = "Opens in the tray each time you sign in to Windows.";

    private readonly ScreenTimeTracker _tracker;

    public SettingsPage(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) return;
            ShowIdleThreshold();
            ShowRunAtLogin();
        };
    }

    private void ShowIdleThreshold()
    {
        IdleThresholdBox.Text = _tracker.IdleThreshold.TotalMinutes.ToString("0.##", CultureInfo.CurrentCulture);
        ShowStatus("");
    }

    private void ShowStatus(string text)
    {
        IdleThresholdStatus.Text = text;
        IdleThresholdStatus.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowRunAtLogin()
    {
        RunAtLoginSwitch.IsChecked = RunAtLogin.IsEnabled;
        // A development build must not register itself, or stop the installed copy registering.
        RunAtLoginSwitch.IsEnabled = RunAtLogin.IsInstalledCopy;
        RunAtLoginHint.Text = RunAtLogin.IsInstalledCopy
            ? RunAtLoginDescription
            : "Only the installed copy (publish.ps1) can start at sign-in.";
    }

    private void RunAtLoginSwitch_Click(object sender, RoutedEventArgs e)
    {
        RunAtLogin.Set(RunAtLoginSwitch.IsChecked == true);
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
            ShowStatus($"Enter minutes from {min:0.##} to {max:0.##}");
            return;
        }

        _tracker.SetIdleThreshold(TimeSpan.FromMinutes(minutes));
        ShowIdleThreshold();
        ShowStatus("Saved");
    }
}
