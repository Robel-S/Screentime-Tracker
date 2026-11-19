using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class AppTrackingTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void App_in_the_foreground_for_ten_minutes_yields_ten_minutes_of_Screen_Time()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(10));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("Notepad", source.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(10), source.ScreenTime);
    }

    [Fact]
    public void Switching_Apps_switches_which_App_the_time_goes_to()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Use(Apps.Code, TimeSpan.FromMinutes(5));
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromMinutes(4), _h.TimeFor(Apps.Notepad));
        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor(Apps.Code));
    }

    [Fact]
    public void Todays_Apps_are_listed_most_time_first()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(2));
        _h.Use(Apps.Spotify, TimeSpan.FromMinutes(7));
        _h.Use(Apps.Code, TimeSpan.FromMinutes(4));

        Assert.Equal(
            ["Spotify", "Visual Studio Code", "Notepad"],
            _h.TodaysSources().Select(s => s.DisplayName));
    }

    [Fact]
    public void Time_with_no_App_in_the_foreground_is_not_Screen_Time()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(2));
        _h.Signals.NothingInFront();
        _h.RunFor(TimeSpan.FromMinutes(30));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal(TimeSpan.FromMinutes(2), source.ScreenTime);
    }

    [Fact]
    public void An_App_is_identified_by_its_executable_whatever_its_case()
    {
        _h.Use(new("NOTEPAD.EXE", "Notepad"), TimeSpan.FromMinutes(2));
        _h.Use(new("notepad.exe", "Windows Notepad"), TimeSpan.FromMinutes(3));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal(TimeSpan.FromMinutes(5), source.ScreenTime);
    }
}
