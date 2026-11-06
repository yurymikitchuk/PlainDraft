using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace PlainDraft.Services;

/// <summary>
/// Shared plain-text snapshot and compact TextPointer map for search and tags.
/// The snapshot is rebuilt lazily after document edits.
/// </summary>
internal sealed class DocumentTextIndex
{
    private readonly List<TextSegment> _segments = [];
    private FlowDocument? _document;
    private bool _isCurrent;

    public string Text { get; private set; } = "";
    public bool HasNonWhitespaceText { get; private set; }

    public void Invalidate() => _isCurrent = false;

    public void EnsureCurrent(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_isCurrent && ReferenceEquals(_document, document))
            return;

        Rebuild(document);
    }

    /// <summary>
    /// Rebuilds the snapshot. The optional callback lets startup normalization
    /// reuse this same document walk instead of traversing the tree again.
    /// </summary>
    public void Rebuild(FlowDocument document, Action<DependencyObject>? elementVisitor = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = new StringBuilder();
        _segments.Clear();
        HasNonWhitespaceText = false;

        var navigator = document.ContentStart;
        var end = document.ContentEnd;
        while (navigator is not null && navigator.CompareTo(end) < 0)
        {
            var context = navigator.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var run = navigator.GetTextInRun(LogicalDirection.Forward);
                if (run.Length > 0)
                {
                    _segments.Add(new TextSegment(text.Length, run.Length, navigator));
                    text.Append(run);
                    if (!HasNonWhitespaceText && ContainsNonWhitespace(run))
                        HasNonWhitespaceText = true;
                }
            }
            else if (context == TextPointerContext.ElementStart)
            {
                if (navigator.GetAdjacentElement(LogicalDirection.Forward) is DependencyObject element)
                {
                    elementVisitor?.Invoke(element);
                    if (element is LineBreak or Paragraph or ListItem)
                        AppendBoundary(text);
                }
            }

            navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
        }

        Text = text.ToString();
        _document = document;
        _isCurrent = true;
    }

    public bool TryGetRange(int startIndex, int length, out TextPointer start, out TextPointer end)
    {
        start = null!;
        end = null!;
        if (length <= 0 || startIndex < 0 || startIndex > Text.Length - length)
            return false;

        var firstSegment = FindSegment(startIndex);
        var lastIndex = startIndex + length - 1;
        var lastSegment = FindSegment(lastIndex);
        if (firstSegment < 0 || lastSegment < 0)
            return false;

        var first = _segments[firstSegment];
        var last = _segments[lastSegment];
        start = first.Start.GetPositionAtOffset(startIndex - first.TextStart, LogicalDirection.Forward)!;
        end = last.Start.GetPositionAtOffset(lastIndex - last.TextStart + 1, LogicalDirection.Forward)!;
        return start is not null && end is not null;
    }

    private int FindSegment(int textIndex)
    {
        var low = 0;
        var high = _segments.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var segment = _segments[middle];
            if (textIndex < segment.TextStart)
            {
                high = middle - 1;
            }
            else if (textIndex >= segment.TextStart + segment.Length)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    private static void AppendBoundary(StringBuilder text)
    {
        if (text.Length == 0 || text[^1] != '\n')
            text.Append('\n');
    }

    private static bool ContainsNonWhitespace(string text)
    {
        foreach (var ch in text)
        {
            if (!char.IsWhiteSpace(ch))
                return true;
        }

        return false;
    }

    private readonly record struct TextSegment(int TextStart, int Length, TextPointer Start);
}
