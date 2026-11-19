using ScreentimeTracker.Core;
using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class CategoryTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void New_Apps_and_Websites_start_Uncategorized()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(2));
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(3));

        Assert.Equal(
            [(SourceKind.Website, Category.Uncategorized), (SourceKind.App, Category.Uncategorized)],
            _h.TodaysSources().Select(s => (s.Kind, s.Category)));
    }

    [Fact]
    public void Recategorizing_a_Source_refiles_its_past_time()
    {
        _h.Use(Apps.Spotify, TimeSpan.FromMinutes(10));

        _h.Categorize(Apps.Spotify, Category.Entertainment);

        var totals = _h.TodaysTotals();
        Assert.Equal(TimeSpan.FromMinutes(10), totals.For(Category.Entertainment));
        Assert.Equal(TimeSpan.Zero, totals.For(Category.Uncategorized));
    }

    [Fact]
    public void A_Source_keeps_its_Category_for_future_time_and_across_restarts()
    {
        _h.Browse(Apps.Edge, "https://github.com/", TimeSpan.FromMinutes(2));
        _h.Categorize("github.com", Category.Productive);
        _h.Restart();
        _h.Browse(Apps.Edge, "https://www.github.com/pulls", TimeSpan.FromMinutes(3));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal(Category.Productive, source.Category);
        Assert.Equal(TimeSpan.FromMinutes(5), _h.TodaysTotals().For(Category.Productive));
    }

    [Fact]
    public void Recategorizing_again_moves_the_time_to_the_new_Category()
    {
        _h.Use(Apps.Code, TimeSpan.FromMinutes(4));
        _h.Categorize(Apps.Code, Category.Entertainment);
        _h.Categorize(Apps.Code, Category.Productive);

        var totals = _h.TodaysTotals();
        Assert.Equal(TimeSpan.FromMinutes(4), totals.For(Category.Productive));
        Assert.Equal(TimeSpan.Zero, totals.For(Category.Entertainment));
    }

    [Fact]
    public void Category_totals_add_up_to_the_days_Screen_Time()
    {
        _h.Use(Apps.Code, TimeSpan.FromMinutes(20));
        _h.Browse(Apps.Brave, "https://www.youtube.com/", TimeSpan.FromMinutes(7));
        _h.Use(Apps.Spotify, TimeSpan.FromMinutes(5));
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Categorize(Apps.Code, Category.Productive);
        _h.Categorize("youtube.com", Category.Entertainment);
        _h.Categorize(Apps.Spotify, Category.Entertainment);

        var totals = _h.TodaysTotals();

        Assert.Equal(TimeSpan.FromMinutes(35), totals.ScreenTime);
        Assert.Equal(TimeSpan.FromMinutes(20), totals.For(Category.Productive));
        Assert.Equal(TimeSpan.FromMinutes(12), totals.For(Category.Entertainment));
        Assert.Equal(TimeSpan.FromMinutes(3), totals.For(Category.Uncategorized));
    }

    [Fact]
    public void Totals_include_time_not_yet_flushed()
    {
        _h.Use(Apps.Code, TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(3), _h.TodaysTotals().ScreenTime);
    }

    [Fact]
    public void Setting_the_Category_of_a_Source_never_seen_does_nothing()
    {
        _h.Tracker.SetCategory(SourceKind.App, "ghost.exe", Category.Productive);
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(1));

        var totals = _h.TodaysTotals();
        Assert.Equal(TimeSpan.FromMinutes(1), totals.ScreenTime);
        Assert.Equal(TimeSpan.Zero, totals.For(Category.Productive));
    }

    [Fact]
    public void Sources_recorded_before_Categories_existed_open_as_Uncategorized_and_can_be_recategorized()
    {
        using var h = TrackerHarness.WithPreCategoriesDatabase();

        var source = Assert.Single(h.TodaysSources());
        Assert.Equal(Category.Uncategorized, source.Category);

        h.Categorize(Apps.Notepad, Category.Productive);
        Assert.Equal(TimeSpan.FromMinutes(6), h.TodaysTotals().For(Category.Productive));
    }
}
