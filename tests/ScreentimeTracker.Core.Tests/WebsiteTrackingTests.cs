using ScreentimeTracker.Core;
using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class WebsiteTrackingTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Brave_in_front_on_www_youtube_com_for_ten_minutes_yields_ten_minutes_on_youtube_com()
    {
        _h.Browse(Apps.Brave, "https://www.youtube.com/watch?v=dQw4w9WgXcQ", TimeSpan.FromMinutes(10));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("youtube.com", source.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(10), source.ScreenTime);
    }

    [Fact]
    public void A_Website_is_its_hostname_whatever_its_case_and_with_or_without_www()
    {
        _h.Browse(Apps.Brave, "https://WWW.YouTube.com/", TimeSpan.FromMinutes(2));
        _h.Browse(Apps.Brave, "http://youtube.com/feed/subscriptions", TimeSpan.FromMinutes(3));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("youtube.com", source.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(5), source.ScreenTime);
    }

    [Fact]
    public void Subdomains_are_different_Websites()
    {
        _h.Browse(Apps.Edge, "https://docs.google.com/document/d/1", TimeSpan.FromMinutes(4));
        _h.Browse(Apps.Edge, "https://mail.google.com/mail/u/0/", TimeSpan.FromMinutes(6));

        Assert.Equal(TimeSpan.FromMinutes(4), _h.TimeFor("docs.google.com"));
        Assert.Equal(TimeSpan.FromMinutes(6), _h.TimeFor("mail.google.com"));
    }

    [Theory]
    [InlineData("edge://settings/profiles")]
    [InlineData("brave://newtab/")]
    [InlineData("chrome-extension://abcdefghijklmnop/options.html")]
    [InlineData("file:///C:/Users/me/notes.html")]
    [InlineData("not a url")]
    public void Browser_time_on_a_page_that_is_not_a_Website_goes_to_the_browser_App(string url)
    {
        _h.Browse(Apps.Brave, url, TimeSpan.FromMinutes(5));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("Brave Browser", source.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(5), source.ScreenTime);
    }

    [Fact]
    public void Browser_time_with_no_Website_goes_to_the_browser_App()
    {
        _h.Browse(Apps.Edge, null, TimeSpan.FromMinutes(5));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("Microsoft Edge", source.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(5), source.ScreenTime);
    }

    [Fact]
    public void Browser_time_goes_to_the_browser_App_when_its_extension_has_never_reported()
    {
        _h.Use(Apps.Brave, TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor(Apps.Brave));
    }

    [Fact]
    public void Browser_time_goes_to_the_browser_App_once_its_extension_stops_reporting()
    {
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(5));
        _h.Tabs.StopReporting(SupportedBrowser.Brave);
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(5) + ScreenTimeTracker.MaxTabReportAge, _h.TimeFor("youtube.com"));
        Assert.Equal(TimeSpan.FromMinutes(10) - ScreenTimeTracker.MaxTabReportAge, _h.TimeFor(Apps.Brave));
    }

    [Fact]
    public void The_Website_is_timed_again_when_the_extension_starts_reporting_again()
    {
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(5));
        _h.Tabs.StopReporting(SupportedBrowser.Brave);
        _h.RunFor(TimeSpan.FromMinutes(10));
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(8) + ScreenTimeTracker.MaxTabReportAge, _h.TimeFor("youtube.com"));
    }

    [Fact]
    public void Switching_tabs_switches_which_Website_the_time_goes_to()
    {
        _h.Browse(Apps.Brave, "https://github.com/", TimeSpan.FromMinutes(3));
        _h.Browse(Apps.Brave, "https://www.reddit.com/", TimeSpan.FromMinutes(5));
        _h.Browse(Apps.Brave, "brave://settings/", TimeSpan.FromMinutes(2));
        _h.Browse(Apps.Brave, "https://github.com/pulls", TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromMinutes(4), _h.TimeFor("github.com"));
        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor("reddit.com"));
        Assert.Equal(TimeSpan.FromMinutes(2), _h.TimeFor(Apps.Brave));
    }

    [Fact]
    public void A_Website_is_only_timed_while_its_browser_is_the_foreground_App()
    {
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(3));
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(3), _h.TimeFor("youtube.com"));
        Assert.Equal(TimeSpan.FromMinutes(10), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void A_tab_in_one_browser_is_not_timed_while_another_browser_is_in_front()
    {
        _h.Tabs.Show(SupportedBrowser.Edge, "https://www.youtube.com/");
        _h.Use(Apps.Brave, TimeSpan.FromMinutes(5));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("Brave Browser", source.DisplayName);
    }

    [Fact]
    public void A_report_for_an_App_that_is_not_a_browser_is_ignored()
    {
        _h.Tabs.Show(SupportedBrowser.Brave, "https://www.youtube.com/");
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(5));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("Notepad", source.DisplayName);
    }

    [Fact]
    public void Websites_are_listed_alongside_Apps_most_time_first()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(2));
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(7));
        _h.Browse(Apps.Brave, null, TimeSpan.FromMinutes(1));
        _h.Use(Apps.Code, TimeSpan.FromMinutes(4));

        Assert.Equal(
            [
                (SourceKind.Website, "youtube.com"),
                (SourceKind.App, "Visual Studio Code"),
                (SourceKind.App, "Notepad"),
                (SourceKind.App, "Brave Browser"),
            ],
            _h.TodaysSources().Select(s => (s.Kind, s.DisplayName)));
    }

    [Fact]
    public void Website_time_survives_a_restart()
    {
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(3));
        _h.Restart();
        _h.Browse(Apps.Brave, "https://youtube.com/", TimeSpan.FromMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor("youtube.com"));
    }

    [Fact]
    public void Website_time_stops_when_the_user_goes_Idle()
    {
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(3));
        _h.Signals.StopInput();
        _h.RunFor(TimeSpan.FromMinutes(30));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal(TimeSpan.FromMinutes(5), source.ScreenTime);
    }
}
