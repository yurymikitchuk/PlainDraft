using System.Printing;

namespace PlainDraft.Services;

public enum PrintScaleMode
{
    FitPage,
    FitWidth,
    ActualSize,
    FillPage
}

public enum PrintOrientationMode
{
    Auto,
    Portrait,
    Landscape
}

public sealed class NotePrintSettings
{
    public required IReadOnlyList<int> Pages { get; init; }
    public PrintQueue? Printer { get; init; }
    public PrintOrientationMode Orientation { get; init; }
    public PrintScaleMode ScaleMode { get; init; }
}
