using System.Windows.Documents;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace PlainDraft.Services;

/// <summary>
/// Hashtags in the document: <c>#Name</c> (letters/digits/_/-), unique names sorted.
/// </summary>
internal static class DocumentTags
{
    public sealed record TagHit(string Name, TextPointer Start, TextPointer End);

    /// <summary>
    /// Unique tag names, sorted by name (case-insensitive), each pointing at the first hit.
    /// </summary>
    public static IReadOnlyList<TagHit> CollectUniqueSorted(FlowDocument document)
    {
        var firstByName = new Dictionary<string, TagHit>(StringComparer.CurrentCultureIgnoreCase);
        var chars = CollectCharsWithBreaks(document);
        for (var i = 0; i < chars.Count; i++)
        {
            if (chars[i].Ch != '#') continue;
            if (!IsTagBoundary(chars, i - 1)) continue;

            var nameStart = i + 1;
            var nameEnd = nameStart;
            while (nameEnd < chars.Count && IsNameChar(chars[nameEnd].Ch))
                nameEnd++;

            if (nameEnd == nameStart) continue;

            var name = BuildName(chars, nameStart, nameEnd);
            if (!firstByName.ContainsKey(name))
            {
                var start = chars[i].At;
                var last = chars[nameEnd - 1].At;
                var end = last.GetPositionAtOffset(1, LogicalDirection.Forward) ?? last;
                firstByName[name] = new TagHit(name, start, end);
            }

            i = nameEnd - 1;
        }

        if (firstByName.Count == 0)
            return Array.Empty<TagHit>();

        return firstByName.Values
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static void JumpTo(RichTextBox editor, TagHit tag)
    {
        editor.Selection.Select(tag.Start, tag.End);
        editor.Focus();
        EditorFormatting.ScrollSelectionIntoView(editor);
    }

    /// <summary>
    /// Like plain text collection, but inserts <c>\n</c> at paragraph / line-break
    /// boundaries so <c>#Tag</c> at the start of a paragraph is not glued to the previous word.
    /// </summary>
    private static List<(char Ch, TextPointer At)> CollectCharsWithBreaks(FlowDocument document)
    {
        var chars = new List<(char Ch, TextPointer At)>();
        if (document is null) return chars;

        var navigator = document.ContentStart;
        var end = document.ContentEnd;

        while (navigator is not null && navigator.CompareTo(end) < 0)
        {
            var ctx = navigator.GetPointerContext(LogicalDirection.Forward);
            if (ctx == TextPointerContext.Text)
            {
                var run = navigator.GetTextInRun(LogicalDirection.Forward);
                for (var i = 0; i < run.Length; i++)
                {
                    var at = navigator.GetPositionAtOffset(i, LogicalDirection.Forward);
                    if (at is null || at.CompareTo(end) >= 0) break;
                    chars.Add((run[i], at));
                }
            }
            else if (ctx == TextPointerContext.ElementStart)
            {
                var el = navigator.GetAdjacentElement(LogicalDirection.Forward);
                if (el is LineBreak or Paragraph or ListItem)
                    chars.Add(('\n', navigator));
            }

            navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
        }

        return chars;
    }

    private static string BuildName(List<(char Ch, TextPointer At)> chars, int start, int end)
    {
        var len = end - start;
        return string.Create(len, (chars, start), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = state.chars[state.start + i].Ch;
        });
    }

    private static bool IsNameChar(char ch) =>
        ch is '_' or '-' || char.IsLetterOrDigit(ch);

    private static bool IsTagBoundary(IReadOnlyList<(char Ch, TextPointer At)> chars, int prevIndex)
    {
        if (prevIndex < 0) return true;
        var prev = chars[prevIndex].Ch;
        if (char.IsWhiteSpace(prev)) return true;
        if (prev is '/' or '#' || IsNameChar(prev)) return false;
        return true;
    }
}
