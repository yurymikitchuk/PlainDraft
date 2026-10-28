using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Color = System.Windows.Media.Color;

namespace PlainDraft.Services;

/// <summary>
/// Keeps body text readable when switching themes: RTF often bakes in black/white runs.
/// Hyperlinks are forced to LinkBrush (including nested runs).
/// </summary>
internal static class EditorTheme
{
    public static void ApplyBodyInk(RichTextBox editor)
    {
        if (editor.Document is null) return;
        if (System.Windows.Application.Current.TryFindResource("InkBrush") is not SolidColorBrush ink)
            return;

        editor.Foreground = ink;
        editor.CaretBrush = ink;
        editor.Document.Foreground = ink;

        ApplyToElement(editor.Document, ink);
    }

    private static void ApplyToElement(DependencyObject node, SolidColorBrush ink)
    {
        if (node is Hyperlink link)
        {
            ApplyLinkBrush(link);
            return;
        }

        if (node is TextElement te)
        {
            var local = te.ReadLocalValue(TextElement.ForegroundProperty);
            if (local is SolidColorBrush brush && IsNeutralBodyColor(brush.Color))
                te.Foreground = ink;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject d)
                ApplyToElement(d, ink);
        }
    }

    private static void ApplyLinkBrush(Hyperlink link)
    {
        if (System.Windows.Application.Current.TryFindResource("LinkBrush") is not SolidColorBrush linkBrush)
            return;

        link.Foreground = linkBrush;
        foreach (var child in LogicalTreeHelper.GetChildren(link))
            StampForeground(child, linkBrush);
    }

    private static void StampForeground(object node, SolidColorBrush brush)
    {
        if (node is TextElement te)
            te.Foreground = brush;

        if (node is not DependencyObject d) return;
        foreach (var child in LogicalTreeHelper.GetChildren(d))
            StampForeground(child, brush);
    }

    private static bool IsNeutralBodyColor(Color c)
    {
        var max = Math.Max(c.R, Math.Max(c.G, c.B));
        var min = Math.Min(c.R, Math.Min(c.G, c.B));
        var chroma = max - min;
        var luma = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

        if (chroma <= 28 && (luma <= 0.35 || luma >= 0.78))
            return true;

        if (Near(c, 0x1A, 0x1A, 0x1A) || Near(c, 0x00, 0x00, 0x00))
            return true;
        if (Near(c, 0xEC, 0xEF, 0xF1) || Near(c, 0xF0, 0xEB, 0xD8) || Near(c, 0xF5, 0xF5, 0xF5))
            return true;
        if (Near(c, 0xB8, 0xB4, 0xAC) || Near(c, 0xC0, 0xBC, 0xB4)
            || Near(c, 0xC8, 0xC8, 0xC8) || Near(c, 0xE0, 0xE0, 0xE0))
            return true;

        return false;
    }

    private static bool Near(Color c, byte r, byte g, byte b, int tol = 12) =>
        Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - b) <= tol;
}
