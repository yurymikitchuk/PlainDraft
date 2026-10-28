using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace PlainDraft.Services;

public sealed record AppTheme(string Id, string DisplayName);

/// <summary>
/// Swaps application brush tokens in place so DynamicResource bindings refresh.
/// </summary>
public static class ThemeService
{
    public const string PaperId = "paper";
    public const string ChalkboardId = "chalkboard";

    public static IReadOnlyList<AppTheme> All { get; } =
    [
        new(PaperId, "Лист бумаги"),
        new(ChalkboardId, "Грифельная доска")
    ];

    public static string NormalizeId(string? id) =>
        string.Equals(id, ChalkboardId, StringComparison.OrdinalIgnoreCase)
            ? ChalkboardId
            : PaperId;

    public static string DisplayName(string? id) =>
        All.FirstOrDefault(t => t.Id == NormalizeId(id))?.DisplayName ?? All[0].DisplayName;

    public static event Action? ThemeChanged;

    public static void Apply(string? themeId)
    {
        var id = NormalizeId(themeId);
        var palette = id == ChalkboardId ? Chalkboard : Paper;
        var rd = System.Windows.Application.Current.Resources;

        Set(rd, "PaperBrush", palette.Paper);
        Set(rd, "ChromeBrush", palette.Chrome);
        Set(rd, "InkBrush", palette.Ink);
        Set(rd, "MutedBrush", palette.Muted);
        Set(rd, "FieldBrush", palette.Field);
        Set(rd, "FieldBorderBrush", palette.FieldBorder);
        Set(rd, "HoverBrush", palette.Hover);
        Set(rd, "PressedBrush", palette.Pressed);
        Set(rd, "ActiveBrush", palette.Active);
        Set(rd, "SeparatorBrush", palette.Separator);
        Set(rd, "PrimaryButtonBrush", palette.Primary);
        Set(rd, "PrimaryButtonHoverBrush", palette.PrimaryHover);
        Set(rd, "PrimaryButtonPressedBrush", palette.PrimaryPressed);
        Set(rd, "SecondaryHoverBrush", palette.SecondaryHover);
        Set(rd, "SecondaryPressedBrush", palette.SecondaryPressed);
        Set(rd, "LinkBrush", palette.Link);
        Set(rd, "ErrorBrush", palette.Error);
        Set(rd, "SelectionBrush", palette.Selection);
        Set(rd, "ScrollThumbBrush", palette.ScrollThumb);

        ThemeChanged?.Invoke();
    }

    private static void Set(ResourceDictionary rd, string key, Color color)
    {
        if (rd[key] is SolidColorBrush existing)
        {
            if (existing.IsFrozen)
            {
                var clone = existing.Clone();
                clone.Color = color;
                rd[key] = clone;
            }
            else
            {
                existing.Color = color;
            }
            return;
        }

        rd[key] = new SolidColorBrush(color);
    }

    private readonly record struct Palette(
        Color Paper,
        Color Chrome,
        Color Ink,
        Color Muted,
        Color Field,
        Color FieldBorder,
        Color Hover,
        Color Pressed,
        Color Active,
        Color Separator,
        Color Primary,
        Color PrimaryHover,
        Color PrimaryPressed,
        Color SecondaryHover,
        Color SecondaryPressed,
        Color Link,
        Color Error,
        Color Selection,
        Color ScrollThumb);

    private static readonly Palette Paper = new(
        Paper: Rgb(0xF2, 0xF2, 0xF2),
        Chrome: Rgb(0xE6, 0xE6, 0xE6),
        Ink: Rgb(0x1A, 0x1A, 0x1A),
        Muted: Rgb(0x8A, 0x8A, 0x8A),
        Field: Rgb(0xFA, 0xFA, 0xFA),
        FieldBorder: Rgb(0xD0, 0xD0, 0xD0),
        Hover: Rgb(0xD8, 0xD8, 0xD8),
        Pressed: Rgb(0xC8, 0xC8, 0xC8),
        Active: Rgb(0xBE, 0xBE, 0xBE),
        Separator: Rgb(0xD0, 0xD0, 0xD0),
        Primary: Rgb(0x8A, 0x8A, 0x8A),
        PrimaryHover: Rgb(0x6E, 0x6E, 0x6E),
        PrimaryPressed: Rgb(0x55, 0x55, 0x55),
        SecondaryHover: Rgb(0xEE, 0xEE, 0xEE),
        SecondaryPressed: Rgb(0xE0, 0xE0, 0xE0),
        Link: Rgb(0x1A, 0x6F, 0xB5),
        Error: Rgb(0xB0, 0x00, 0x20),
        Selection: Rgb(0x90, 0x90, 0x90),
        ScrollThumb: Rgb(0xBE, 0xBE, 0xBE));

    // «Грифельная доска» — paper #242424, elevated surfaces + soft chalk ink.
    private static readonly Palette Chalkboard = new(
        Paper: Rgb(0x24, 0x24, 0x24),
        Chrome: Rgb(0x30, 0x30, 0x30),
        Ink: Rgb(0xC0, 0xBC, 0xB4),
        Muted: Rgb(0x8A, 0x86, 0x80),
        Field: Rgb(0x2A, 0x2A, 0x2A),
        FieldBorder: Rgb(0x3A, 0x3A, 0x3A),
        Hover: Rgb(0x3C, 0x3C, 0x3C),
        Pressed: Rgb(0x42, 0x42, 0x42),
        Active: Rgb(0x48, 0x48, 0x48),
        Separator: Rgb(0x3A, 0x3A, 0x3A),
        Primary: Rgb(0x6A, 0x6A, 0x6A),
        PrimaryHover: Rgb(0x7A, 0x7A, 0x7A),
        PrimaryPressed: Rgb(0x5A, 0x5A, 0x5A),
        SecondaryHover: Rgb(0x3C, 0x3C, 0x3C),
        SecondaryPressed: Rgb(0x42, 0x42, 0x42),
        Link: Rgb(0x8A, 0xA8, 0xC0),
        Error: Rgb(0xE0, 0x80, 0x80),
        Selection: Rgb(0x78, 0x74, 0x6C),
        ScrollThumb: Rgb(0x5A, 0x58, 0x52));

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
