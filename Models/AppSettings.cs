using System.Windows;

namespace PlainDraft.Models;

public sealed class AppSettings
{
    public const string DefaultHotkeyGoToNote = "Ctrl+Shift+Q";
    public const string DefaultHotkeyToggleVisible = "Ctrl+Shift+H";

    public string HotkeyGoToNote { get; set; } = DefaultHotkeyGoToNote;
    public string HotkeyToggleVisible { get; set; } = DefaultHotkeyToggleVisible;
    /// <summary>Theme id: paper | chalkboard.</summary>
    public string ThemeId { get; set; } = "paper";
    public WindowGeometry Window { get; set; } = new();
}

public sealed class WindowGeometry
{
    /// <summary>A4 portrait mm: width / height.</summary>
    private const double A4Ratio = 210.0 / 297.0;

    public double Width { get; set; }
    public double Height { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }

    public WindowGeometry()
    {
        var (w, h) = ComputeDefaultSize();
        Width = w;
        Height = h;
    }

    /// <summary>
    /// Height = 2/3 of work-area height; width from A4 portrait (210×297).
    /// Example: 1080p → 720×509.
    /// </summary>
    public static (double Width, double Height) ComputeDefaultSize()
    {
        var work = SystemParameters.WorkArea;
        var height = work.Height * (2.0 / 3.0);
        var width = height * A4Ratio;

        // Fit into the work area while keeping A4 proportions.
        if (width > work.Width)
        {
            width = work.Width;
            height = width / A4Ratio;
        }

        if (height > work.Height)
        {
            height = work.Height;
            width = height * A4Ratio;
        }

        // Respect MainWindow min size (450×285).
        width = Math.Max(width, 450);
        height = Math.Max(height, 285);

        return (Math.Round(width), Math.Round(height));
    }
}
