using System.ComponentModel;
using ScreentimeTracker.App.Pages;
using ScreentimeTracker.Core;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace ScreentimeTracker.App;

/// <summary>
/// The main window: a sidebar between the Overview, Timers and Settings pages, opening on the Overview.
/// Closing it hides it; the app goes on in the tray.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool _quitting;

    public MainWindow(ScreenTimeTracker tracker)
    {
        InitializeComponent();
        ThemeFollower.Follow(this);
        Navigation.SetPageProviderService(new PageProvider(
            new OverviewPage(tracker), new TimersPage(tracker), new SettingsPage(tracker)));
        Loaded += (_, _) => Navigation.Navigate(typeof(OverviewPage));
    }

    public void CloseForQuit()
    {
        _quitting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_quitting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    /// <summary>Hands the sidebar the one instance of each page, so a page keeps its state between visits.</summary>
    private sealed class PageProvider(params object[] pages) : INavigationViewPageProvider
    {
        public object? GetPage(Type pageType) => pages.FirstOrDefault(pageType.IsInstanceOfType);
    }
}
