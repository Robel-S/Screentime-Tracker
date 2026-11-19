using ScreentimeTracker.Core;
using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class DayTests
{
    private static readonly DateTime BeforeMidnight = new(2026, 3, 10, 23, 50, 0);

    [Fact]
    public void Time_spanning_midnight_is_split_between_the_two_Days()
    {
        using var h = new TrackerHarness(start: BeforeMidnight);

        h.Use(Apps.Code, TimeSpan.FromMinutes(25));

        Assert.Equal(TimeSpan.FromMinutes(10), h.TimeOn(h.Yesterday, Apps.Code));
        Assert.Equal(TimeSpan.FromMinutes(15), h.TimeOn(h.Today, Apps.Code));
        Assert.Equal(TimeSpan.FromMinutes(15), h.TodaysTotals().ScreenTime);
    }

    [Fact]
    public void The_7_Day_report_has_each_Days_totals_oldest_first_including_Days_with_no_Screen_Time()
    {
        using var h = new TrackerHarness();
        h.Use(Apps.Code, TimeSpan.FromMinutes(30));
        h.Categorize(Apps.Code, Category.Productive);
        h.SleepFor(TimeSpan.FromDays(2));
        h.Use(Apps.Spotify, TimeSpan.FromMinutes(12));
        h.Categorize(Apps.Spotify, Category.Entertainment);
        h.SleepFor(TimeSpan.FromDays(1));
        h.Use(Apps.Notepad, TimeSpan.FromMinutes(4));

        var days = h.Tracker.Last7Days(h.Today).Days;

        Assert.Equal(
            Enumerable.Range(0, 7).Select(i => h.Today.AddDays(i - 6)),
            days.Select(d => d.Day));
        Assert.Equal(
            [0, 0, 0, 30, 0, 12, 4],
            days.Select(d => d.ScreenTime.TotalMinutes));
        Assert.Equal(TimeSpan.FromMinutes(30), days[3].For(Category.Productive));
        Assert.Equal(TimeSpan.FromMinutes(12), days[5].For(Category.Entertainment));
        Assert.Equal(TimeSpan.FromMinutes(4), days[6].For(Category.Uncategorized));
        Assert.Equal(TimeSpan.Zero, days[4].For(Category.Productive));
    }

    [Fact]
    public void The_7_Day_report_lists_Sources_over_the_whole_period_most_first()
    {
        using var h = new TrackerHarness();
        h.Use(Apps.Notepad, TimeSpan.FromMinutes(20)); // seven Days back: outside the period
        h.SleepFor(TimeSpan.FromDays(1));
        h.Use(Apps.Spotify, TimeSpan.FromMinutes(10));
        h.Use(Apps.Code, TimeSpan.FromMinutes(5));
        h.SleepFor(TimeSpan.FromDays(6));
        h.Use(Apps.Code, TimeSpan.FromMinutes(8));

        var sources = h.Tracker.Last7Days(h.Today).Sources;

        Assert.Equal(
            [(Apps.Code.DisplayName, 13.0), (Apps.Spotify.DisplayName, 10.0)],
            sources.Select(s => (s.DisplayName, s.ScreenTime.TotalMinutes)));
    }

    [Fact]
    public void Recategorizing_a_Source_refiles_its_time_across_the_7_Days()
    {
        using var h = new TrackerHarness();
        h.Use(Apps.Spotify, TimeSpan.FromMinutes(10));
        h.SleepFor(TimeSpan.FromDays(1));
        h.Use(Apps.Spotify, TimeSpan.FromMinutes(5));

        var spotify = h.Tracker.Last7Days(h.Today).Sources.Single();
        h.Tracker.SetCategory(spotify.Kind, spotify.Key, Category.Entertainment);

        var report = h.Tracker.Last7Days(h.Today);
        Assert.Equal(Category.Entertainment, report.Sources.Single().Category);
        Assert.Equal(
            [0, 0, 0, 0, 0, 10, 5],
            report.Days.Select(d => d.For(Category.Entertainment).TotalMinutes));
    }

    [Fact]
    public void The_7_Day_report_splits_time_still_running_across_midnight()
    {
        using var h = new TrackerHarness(start: BeforeMidnight);
        h.Use(Apps.Code, TimeSpan.FromMinutes(13));

        var report = h.Tracker.Last7Days(h.Today);

        Assert.Equal(TimeSpan.FromMinutes(10), report.Days[5].ScreenTime);
        Assert.Equal(TimeSpan.FromMinutes(3), report.Days[6].ScreenTime);
        Assert.Equal(TimeSpan.FromMinutes(13), report.Sources.Single().ScreenTime);
        Assert.Equal(TimeSpan.FromMinutes(13), report.ScreenTime);
    }
}
