using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace PlainDraft.Services;

internal static class ToolbarMenus
{
    public static ContextMenu Create(FrameworkElement resourceHost, bool scrollable = false)
    {
        var menu = new ContextMenu();
        var styleKey = scrollable ? "ToolbarContextMenuScrollable" : "ToolbarContextMenu";
        if (resourceHost.TryFindResource(styleKey) is Style style)
            menu.Style = style;
        if (scrollable && resourceHost.TryFindResource("PlainScrollBar") is Style scrollStyle)
            menu.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollStyle;
        return menu;
    }

    public static void Open(
        ContextMenu menu,
        UIElement target,
        PlacementMode placement = PlacementMode.Top)
    {
        menu.PlacementTarget = target;
        menu.Placement = placement;
        // Popup template has MacShadowPopupMargin 28 all around. Cancel the
        // facing-side inset so the chrome sits faceGap px from the button —
        // same gap for Top (Headers/Lists) and Bottom (Tags).
        var inset = MacShadowPopupInset();
        const double faceGap = 12;
        menu.HorizontalOffset = -inset;
        menu.VerticalOffset = placement == PlacementMode.Bottom
            ? -inset + faceGap
            : inset - faceGap;
        menu.IsOpen = true;
    }

    public static double MacShadowPopupInset()
    {
        if (System.Windows.Application.Current?.TryFindResource("MacShadowPopupInset") is double inset)
            return inset;
        return 28;
    }

    public static MenuItem Item(FrameworkElement resourceHost, string header, Action action)
    {
        var item = new MenuItem { Header = header, Icon = null };
        ApplyItemStyle(resourceHost, item);
        item.Click += (_, _) => action();
        return item;
    }

    public static MenuItem HeadingItem(
        FrameworkElement resourceHost,
        string text,
        double fontSize,
        FontWeight weight,
        Action action)
    {
        var ink = resourceHost.TryFindResource("InkBrush") as Brush
                  ?? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        var item = new MenuItem
        {
            Icon = null,
            Header = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = weight,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = ink,
                VerticalAlignment = VerticalAlignment.Center
            },
            Padding = new Thickness(14, fontSize >= 20 ? 10 : 6, 14, fontSize >= 20 ? 10 : 6)
        };
        ApplyItemStyle(resourceHost, item);
        item.Click += (_, _) => action();
        return item;
    }

    private static void ApplyItemStyle(FrameworkElement resourceHost, MenuItem item)
    {
        if (resourceHost.TryFindResource("ToolbarMenuItem") is Style style)
            item.Style = style;
    }
}
