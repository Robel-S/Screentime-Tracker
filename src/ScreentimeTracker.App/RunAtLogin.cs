using System.IO;
using Microsoft.Win32;

namespace ScreentimeTracker.App;

/// <summary>
/// Whether the app starts when the user signs in, through the current user's Run registry key. The
/// installed copy turns this on the first time it runs; after that the user's choice in Settings stands.
/// </summary>
internal static class RunAtLogin
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ScreentimeTracker";

    /// <summary>Where the app keeps its own preferences, apart from the tracked data.</summary>
    private const string AppKeyPath = @"Software\ScreentimeTracker";
    private const string ChosenValueName = "RunAtLoginChosen";

    /// <summary>Where publish.ps1 installs the app.</summary>
    public static readonly string InstalledExePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "ScreentimeTracker", "ScreentimeTracker.exe");

    public static bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return run?.GetValue(RunValueName) is string;
        }
    }

    /// <summary>Whether this is the installed copy, the only one that can start at sign-in.</summary>
    public static bool IsInstalledCopy =>
        string.Equals(Environment.ProcessPath, InstalledExePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>Turns starting the installed copy at sign-in on or off, and remembers that the user chose.</summary>
    public static void Set(bool enabled)
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKeyPath))
        {
            if (enabled) run.SetValue(RunValueName, $"\"{InstalledExePath}\"");
            else run.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        using var app = Registry.CurrentUser.CreateSubKey(AppKeyPath);
        app.SetValue(ChosenValueName, 1, RegistryValueKind.DWord);
    }

    /// <summary>
    /// On startup of the installed copy: turns starting at sign-in on the first time, and otherwise keeps
    /// an enabled entry pointing at this copy. A development build never registers itself.
    /// </summary>
    public static void ApplyOnStartup()
    {
        if (!IsInstalledCopy) return;

        bool chosen;
        using (var app = Registry.CurrentUser.OpenSubKey(AppKeyPath))
        {
            chosen = app?.GetValue(ChosenValueName) is int;
        }
        if (!chosen || IsEnabled) Set(true);
    }
}
