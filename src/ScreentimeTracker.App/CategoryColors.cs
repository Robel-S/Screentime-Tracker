using System.Windows;
using System.Windows.Media;
using ScreentimeTracker.Core;
using Wpf.Ui.Appearance;

namespace ScreentimeTracker.App;

/// <summary>
/// Each Category's colour, kept as application brushes so the charts, totals and Category picker all
/// use the same ones, retuned whenever the app's light/dark theme changes. Refer to them by
/// <see cref="BrushKey"/> through a dynamic resource reference so they follow the theme.
/// </summary>
internal static class CategoryColors
{
    /// <summary>The ring or slot shown where there is no Screen Time to chart.</summary>
    public const string EmptyBrushKey = "EmptyChartBrush";

    /// <summary>The Categories in chart order: stacked bottom-up and drawn clockwise from the top.</summary>
    public static readonly IReadOnlyList<Category> ChartOrder =
        [Category.Productive, Category.Entertainment, Category.Uncategorized];

    /// <summary>The resource key of <paramref name="category"/>'s brush.</summary>
    public static string BrushKey(Category category) => category switch
    {
        Category.Productive => "ProductiveBrush",
        Category.Entertainment => "EntertainmentBrush",
        Category.Uncategorized => "UncategorizedBrush",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>Sets the brushes for the current theme, and again each time it changes.</summary>
    public static void Follow(Application app)
    {
        Apply(app, ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += (theme, _) => Apply(app, theme);
    }

    private static void Apply(Application app, ApplicationTheme theme)
    {
        var dark = theme == ApplicationTheme.Dark;
        Set(app, BrushKey(Category.Productive), dark ? "#4CA2F5" : "#0F6CBD");
        Set(app, BrushKey(Category.Entertainment), dark ? "#F1707B" : "#D13438");
        Set(app, BrushKey(Category.Uncategorized), dark ? "#8F8F8F" : "#8A8886");
        Set(app, EmptyBrushKey, dark ? "#3B3B3B" : "#E1DFDD");
    }

    private static void Set(Application app, string key, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        app.Resources[key] = brush;
    }
}
