namespace ScreentimeTracker.Core;

/// <summary>A desktop program in the foreground, as reported by the operating system.</summary>
/// <param name="Executable">The program's executable file name, for example "notepad.exe". This identifies the App.</param>
/// <param name="DisplayName">A readable name to show for the App, for example "Notepad".</param>
/// <param name="IsTracker">Whether this is the Screentime Tracker itself, whose time is never Screen Time.</param>
public sealed record AppIdentity(string Executable, string DisplayName, bool IsTracker = false);

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

/// <summary>Port: the current local time.</summary>
public interface IClock
{
    DateTime Now { get; }
}
