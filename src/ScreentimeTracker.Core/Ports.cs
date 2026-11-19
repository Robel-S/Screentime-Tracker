namespace ScreentimeTracker.Core;

/// <summary>A browser whose extension reports the Website in its active tab.</summary>
public enum SupportedBrowser
{
    Edge,
    Brave,
}

/// <summary>A desktop program in the foreground, as reported by the operating system.</summary>
/// <param name="Executable">The program's executable file name, for example "notepad.exe". This identifies the App.</param>
/// <param name="DisplayName">A readable name to show for the App, for example "Notepad".</param>
/// <param name="IsTracker">Whether this is the Screentime Tracker itself, whose time is never Screen Time.</param>
/// <param name="Browser">Which supported browser this is, or null when it is not one.</param>
public sealed record AppIdentity(string Executable, string DisplayName, bool IsTracker = false, SupportedBrowser? Browser = null);

/// <summary>Port: what the operating system currently says about the user's session.</summary>
public interface ISystemSignals
{
    /// <summary>The App in the foreground, or null when there is none.</summary>
    AppIdentity? ForegroundApp { get; }

    /// <summary>Whether the foreground window covers its whole screen. A fullscreen window means the user is never Idle.</summary>
    bool ForegroundIsFullscreen { get; }

    /// <summary>How long since the last keyboard or mouse input.</summary>
    TimeSpan SinceLastInput { get; }

    /// <summary>Whether the session is unlocked and awake. False while locked, asleep or logged out.</summary>
    bool IsSessionActive { get; }
}

/// <summary>What a browser's extension last said about its active tab.</summary>
/// <param name="Url">The active tab's raw URL, or null when there is no Website (for example a settings page).</param>
/// <param name="ReportedAt">When the report arrived, in local time.</param>
public sealed record TabReport(string? Url, DateTime ReportedAt);

/// <summary>Port: the latest active tab report from each supported browser's extension.</summary>
public interface IActiveTabFeed
{
    /// <summary>The latest report from the browser, or null if it has never reported.</summary>
    TabReport? LatestFrom(SupportedBrowser browser);
}

/// <summary>Port: the current local time.</summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>Port: puts the Interruption on screen and takes it away.</summary>
public interface IInterruptionPresenter
{
    /// <summary>Shows the Interruption, which stays until <see cref="Hide"/>.</summary>
    void Show();

    /// <summary>Takes the Interruption off screen.</summary>
    void Hide();
}
