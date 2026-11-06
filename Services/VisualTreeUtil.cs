using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PlainDraft.Services;

internal static class VisualTreeUtil
{
    public static T? FindParent<T>(DependencyObject? child) where T : class
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = GetParent(child);
        }
        return null;
    }

    public static T? FindDescendant<T>(DependencyObject? root) where T : class
    {
        if (root is null) return null;
        if (root is T match) return match;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindDescendant<T>(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }

        return null;
    }

    public static DependencyObject? GetParent(DependencyObject child)
    {
        if (child is FrameworkContentElement fce)
            return fce.Parent;
        if (child is Visual or Visual3D)
            return VisualTreeHelper.GetParent(child);
        return LogicalTreeHelper.GetParent(child);
    }
}
