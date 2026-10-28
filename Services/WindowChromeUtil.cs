using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Color = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;

namespace PlainDraft.Services;

internal static class WindowChromeUtil
{
    private static readonly Color DefaultPaper = Color.FromRgb(0xF2, 0xF2, 0xF2);

    /// <summary>Main window: DWM shadow; frame tinted to paper.</summary>
    public static void ApplyPaperChrome(Window window)
    {
        var paper = window.TryFindResource("PaperBrush") is SolidColorBrush brush
            ? brush.Color
            : DefaultPaper;
        DwmHelper.ApplySystemChrome(window, paper, systemBorder: false);
    }

    /// <summary>Dialogs: same chrome, but OS-default system frame color.</summary>
    public static void ApplyDialogChrome(Window window)
    {
        var paper = window.TryFindResource("PaperBrush") is SolidColorBrush brush
            ? brush.Color
            : DefaultPaper;
        DwmHelper.ApplySystemChrome(window, paper, systemBorder: true);
        ApplyWin10DialogOutline(window);
    }

    /// <summary>
    /// Win10 has no DWMWA_BORDER_COLOR; draw a 1px client outline so dialogs
    /// do not blend into the desktop. Win11 keeps DWM frame only.
    /// </summary>
    private static void ApplyWin10DialogOutline(Window window)
    {
        if (window.Content is not Border border) return;

        if (DwmHelper.HasWin11FrameChrome)
        {
            border.BorderThickness = new Thickness(0);
            border.ClearValue(Border.BorderBrushProperty);
            return;
        }

        // BorderThickness is inside the client area. Fixed-width dialogs
        // (content 340 + margins 40 = window 380) would clip by 2px on Win10.
        const double outline = 1;
        var alreadyOutlined = Math.Abs(border.BorderThickness.Left - outline) < 0.1;
        border.BorderThickness = new Thickness(outline);
        border.SetResourceReference(Border.BorderBrushProperty, "FieldBorderBrush");

        // Settings re-applies chrome on theme change — grow only once.
        if (alreadyOutlined) return;

        var grow = outline * 2;
        if (!double.IsNaN(window.Width) && window.Width > 0)
            window.Width += grow;
        if (window.MinWidth > 0)
            window.MinWidth += grow;
    }

    /// <summary>Focus ring for DialogFieldBorder — same on Link / Settings / Print.</summary>
    public static void SetDialogFieldFocus(FrameworkElement host, Border border, bool focused)
    {
        var idle = host.TryFindResource("FieldBorderBrush") as Brush
                   ?? new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
        var focus = host.TryFindResource("InkBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        border.BorderBrush = focused ? focus : idle;
        border.BorderThickness = new Thickness(focused ? 1.5 : 1);
    }

    /// <summary>
    /// Drag the borderless window unless the click started on interactive chrome.
    /// </summary>
    public static void TryDrag(Window window, MouseButtonEventArgs e, bool excludeEditor = false)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount > 1) return;

        var source = e.OriginalSource as DependencyObject;
        if (VisualTreeUtil.FindParent<TextBox>(source) is not null) return;
        if (VisualTreeUtil.FindParent<ButtonBase>(source) is not null) return;
        if (excludeEditor && VisualTreeUtil.FindParent<System.Windows.Controls.RichTextBox>(source) is not null)
            return;
        if (excludeEditor && VisualTreeUtil.FindParent<System.Windows.Documents.Hyperlink>(source) is not null)
            return;

        try { window.DragMove(); } catch { /* ignore */ }
    }
}
