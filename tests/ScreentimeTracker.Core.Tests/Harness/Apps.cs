using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

public static class Apps
{
    public static readonly AppIdentity Notepad = new("notepad.exe", "Notepad");
    public static readonly AppIdentity Code = new("Code.exe", "Visual Studio Code");
    public static readonly AppIdentity Spotify = new("Spotify.exe", "Spotify");
    public static readonly AppIdentity ScreentimeTracker = new("ScreentimeTracker.App.exe", "Screentime Tracker", IsTracker: true);
    public static readonly AppIdentity Brave = new("brave.exe", "Brave Browser", Browser: SupportedBrowser.Brave);
    public static readonly AppIdentity Edge = new("msedge.exe", "Microsoft Edge", Browser: SupportedBrowser.Edge);
}
