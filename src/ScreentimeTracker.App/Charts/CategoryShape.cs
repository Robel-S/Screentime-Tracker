using System.Windows.Controls;
using System.Windows.Shapes;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App.Charts;

/// <summary>Readies a chart shape for one Category: its colour, a hover highlight and a tooltip.</summary>
internal static class CategoryShape
{
    /// <summary>Colours <paramref name="shape"/> for <paramref name="category"/>; returns the tooltip text to fill in.</summary>
    public static TextBlock Prepare(Shape shape, Category category)
    {
        var tip = new TextBlock();
        shape.ToolTip = new ToolTip { Content = tip };
        ToolTipService.SetInitialShowDelay(shape, 0);
        shape.SetResourceReference(Shape.FillProperty, CategoryColors.BrushKey(category));
        shape.MouseEnter += (_, _) => shape.Opacity = 0.8;
        shape.MouseLeave += (_, _) => shape.Opacity = 1;
        return tip;
    }
}
