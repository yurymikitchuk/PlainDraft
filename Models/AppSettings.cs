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
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 594;
    public double? X { get; set; }
    public double? Y { get; set; }
}
