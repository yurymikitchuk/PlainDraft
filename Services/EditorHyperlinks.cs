using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Point = System.Windows.Point;

namespace PlainDraft.Services;

internal static class EditorHyperlinks
{
    public const string OpenTooltip = "Ctrl+клик — открыть ссылку";

    public static void Hook(FlowDocument document)
    {
        SolidColorBrush? linkBrush = null;
        if (System.Windows.Application.Current.TryFindResource("LinkBrush") is SolidColorBrush brush)
            linkBrush = brush;

        foreach (var link in Enumerate(document))
        {
            link.Cursor = System.Windows.Input.Cursors.IBeam;
            link.ToolTip ??= OpenTooltip;
            if (linkBrush is null) continue;
            link.Foreground = linkBrush;
            foreach (var child in LogicalTreeHelper.GetChildren(link))
                StampForeground(child, linkBrush);
        }
    }

    private static void StampForeground(object node, SolidColorBrush brush)
    {
        if (node is TextElement te)
            te.Foreground = brush;
        if (node is not DependencyObject d) return;
        foreach (var child in LogicalTreeHelper.GetChildren(d))
            StampForeground(child, brush);
    }

    public static bool TryOpenOnCtrlClick(RichTextBox editor, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return false;
        if (e.ChangedButton != MouseButton.Left) return false;

        var link = Find(e.OriginalSource as DependencyObject)
                   ?? AtPoint(editor, e.GetPosition(editor));
        if (link?.NavigateUri is null) return false;

        UriLauncher.Open(link.NavigateUri);
        e.Handled = true;
        return true;
    }

    public static void UpdateCursor(RichTextBox editor, DependencyObject? originalSource)
    {
        var link = Find(originalSource);
        if (link is null && editor.IsMouseOver)
            link = AtPoint(editor, Mouse.GetPosition(editor));

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var overLink = link?.NavigateUri is not null;

        if (link is not null)
            link.Cursor = ctrl
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.IBeam;

        if (ctrl && overLink)
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
        else
            ClearCursorOverride();
    }

    public static void ClearCursorOverride()
    {
        if (Mouse.OverrideCursor is not null)
            Mouse.OverrideCursor = null;
    }

    public static Hyperlink? AtPoint(RichTextBox editor, Point point)
    {
        var pointer = editor.GetPositionFromPoint(point, snapToText: true);
        return Find(pointer?.Parent);
    }

    public static Hyperlink? Find(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is Hyperlink link) return link;
            node = VisualTreeUtil.GetParent(node);
        }
        return null;
    }

    public static IEnumerable<Hyperlink> Enumerate(DependencyObject root)
    {
        if (root is Hyperlink h) yield return h;
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject d)
            {
                foreach (var nested in Enumerate(d))
                    yield return nested;
            }
        }
    }
}
