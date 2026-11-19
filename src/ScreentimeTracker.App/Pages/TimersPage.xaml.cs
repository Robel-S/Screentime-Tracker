using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ScreentimeTracker.Core;
using Ui = Wpf.Ui.Controls;

namespace ScreentimeTracker.App.Pages;

/// <summary>Starts Timers, and lists every Timer with its time left and a Cancel button. Ticks while visible.</summary>
public partial class TimersPage : Page
{
    private readonly ScreenTimeTracker _tracker;
    private readonly DispatcherTimer _ticker;
    /// <summary>Each Timer's name and status text, in the order the cards are listed.</summary>
    private readonly List<(TimerId Id, TextBlock Name, TextBlock Status)> _cards = [];

    public TimersPage(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        Starter.StartRequested += (duration, label) =>
        {
            _tracker.StartTimer(duration, label);
            ShowTimers();
        };
        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ticker.Tick += (_, _) => ShowTimers();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { ShowTimers(); _ticker.Start(); }
            else _ticker.Stop();
        };
    }

    private void ShowTimers()
    {
        var timers = _tracker.Timers;
        // Rebuild the cards only when the Timers change, so a Cancel button isn't swapped out mid-click.
        if (!timers.Select(t => t.Id).SequenceEqual(_cards.Select(c => c.Id)))
        {
            TimerList.Children.Clear();
            _cards.Clear();
            foreach (var timer in timers) TimerList.Children.Add(Card(timer.Id));
        }
        for (var i = 0; i < timers.Count; i++)
        {
            _cards[i].Name.Text = timers[i].Name;
            _cards[i].Status.Text = TimerText.Status(timers[i]);
        }
        NoTimersText.Text = timers.Count == 0 ? TimerText.NoTimers : "";
        NoTimersText.Visibility = timers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CancelAllButton.Visibility = timers.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private Border Card(TimerId id)
    {
        var name = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var status = new TextBlock { FontSize = 14, Margin = new Thickness(0, 2, 0, 0) };
        status.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var cancel = new Ui.Button { Content = "Cancel", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        cancel.Click += (_, _) =>
        {
            _tracker.CancelTimer(id);
            ShowTimers();
        };

        var text = new StackPanel();
        text.Children.Add(name);
        text.Children.Add(status);
        var row = new DockPanel();
        DockPanel.SetDock(cancel, Dock.Right);
        row.Children.Add(cancel);
        row.Children.Add(text);
        _cards.Add((id, name, status));

        var card = new Border { Padding = new Thickness(20, 14, 20, 14), Margin = new Thickness(0, 0, 0, 8), Child = row };
        card.SetResourceReference(StyleProperty, "Card");
        return card;
    }

    private void CancelAll_Click(object sender, RoutedEventArgs e)
    {
        _tracker.CancelAllTimers();
        ShowTimers();
    }
}
