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
    public static IReadOnlyList<TagHit> CollectUniqueSorted(
        DocumentTextIndex textIndex,
        FlowDocument document)
    {
        textIndex.EnsureCurrent(document);
        var text = textIndex.Text;
        var firstByName = new Dictionary<string, TagHit>(StringComparer.CurrentCultureIgnoreCase);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '#') continue;
            if (!IsTagBoundary(text, i - 1)) continue;

            var nameStart = i + 1;
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsNameChar(text[nameEnd]))
                nameEnd++;

            if (nameEnd == nameStart) continue;

            var name = text[nameStart..nameEnd];
            if (!firstByName.ContainsKey(name) &&
                textIndex.TryGetRange(i, nameEnd - i, out var start, out var end))
            {
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

    private static bool IsNameChar(char ch) =>
        ch is '_' or '-' || char.IsLetterOrDigit(ch);

    private static bool IsTagBoundary(string text, int prevIndex)
    {
        if (prevIndex < 0) return true;
        var prev = text[prevIndex];
        if (char.IsWhiteSpace(prev)) return true;
        if (prev is '/' or '#' || IsNameChar(prev)) return false;
        return true;
    }
}
