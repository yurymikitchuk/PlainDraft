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

    public static DependencyObject? GetParent(DependencyObject child)
    {
        if (child is FrameworkContentElement fce)
            return fce.Parent;
        if (child is Visual or Visual3D)
            return VisualTreeHelper.GetParent(child);
        return LogicalTreeHelper.GetParent(child);
    }
}
