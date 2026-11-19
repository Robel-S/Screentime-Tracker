using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ScreentimeTracker.App;

/// <summary>Keeps a window in the Windows light/dark setting, including changes made while it is open.</summary>
internal static class ThemeFollower
{
    public static void Follow(Window window)
    {
        window.Loaded += (_, _) => SystemThemeWatcher.Watch(window, WindowBackdropType.Mica);
        // The watcher hears of a change only through a window that exists; the app starts to the tray,
        // so catch up on any change made while no window was open.
        window.IsVisibleChanged += (_, _) =>
        {
            if (window.IsVisible && !ApplicationThemeManager.IsAppMatchesSystem())
                ApplicationThemeManager.ApplySystemTheme();
        };
    }
}
