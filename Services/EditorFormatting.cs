using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;

namespace PlainDraft.Services;

/// <summary>
/// Formatting, find, and style helpers for the note RichTextBox.
/// </summary>
public static class EditorFormatting
{
    public const double NormalFontSize = 14;
    public static readonly FontFamily NormalFontFamily = new("Segoe UI");

    /// <summary>
    /// «Обычный текст» — default typing style after clearing the document or via toolbar.
    /// </summary>
    public static void ApplyNormalTypingStyle(RichTextBox editor, bool focus = true)
    {
        if (focus) editor.Focus();
        var selection = editor.Selection;
        selection.ApplyPropertyValue(TextElement.FontFamilyProperty, NormalFontFamily);
        selection.ApplyPropertyValue(TextElement.FontSizeProperty, NormalFontSize);
        selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
        selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
        selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
        selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
    }

    /// <summary>Clears TextElement.Background on the whole document.</summary>
    public static void ClearAllTextBackgrounds(FlowDocument document)
    {
        if (document is null) return;
        new TextRange(document.ContentStart, document.ContentEnd)
            .ApplyPropertyValue(TextElement.BackgroundProperty, null);
    }

    public static void ScrollSelectionIntoView(RichTextBox editor)
    {
        var rect = editor.Selection.Start.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty) return;
        editor.ScrollToVerticalOffset(
            Math.Max(0, editor.VerticalOffset + rect.Top - editor.ActualHeight / 3));
    }

    public static bool IsDocumentEffectivelyEmpty(FlowDocument document)
    {
        var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
        return string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// After Select-All + Delete, leftover empty paragraphs keep heading formatting.
    /// Reset caret and empty paragraphs to normal text.
    /// </summary>
    public static void EnsureNormalStyleWhenEmpty(RichTextBox editor)
    {
        if (!IsDocumentEffectivelyEmpty(editor.Document)) return;

        if (editor.Document.Blocks.Count == 0)
        {
            editor.Document.Blocks.Add(CreateNormalParagraph());
        }
        else
        {
            foreach (var block in editor.Document.Blocks)
            {
                if (block is not Paragraph paragraph) continue;
                paragraph.FontFamily = NormalFontFamily;
                paragraph.FontSize = NormalFontSize;
                paragraph.FontWeight = FontWeights.Normal;
                paragraph.FontStyle = FontStyles.Normal;
                foreach (var inline in paragraph.Inlines)
                {
                    inline.FontFamily = NormalFontFamily;
                    inline.FontSize = NormalFontSize;
                    inline.FontWeight = FontWeights.Normal;
                    inline.FontStyle = FontStyles.Normal;
                    inline.TextDecorations = null;
                }
            }
        }

        ApplyNormalTypingStyle(editor, focus: false);
    }

    public static Paragraph CreateNormalParagraph() =>
        new(new Run(string.Empty))
        {
            FontFamily = NormalFontFamily,
            FontSize = NormalFontSize,
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Normal
        };

    public static void ToggleBold(RichTextBox editor)
    {
        editor.Focus();
        var selection = editor.Selection;
        selection.ApplyPropertyValue(
            TextElement.FontWeightProperty,
            IsBold(selection) ? FontWeights.Normal : FontWeights.Bold);
    }

    public static void ToggleItalic(RichTextBox editor)
    {
        editor.Focus();
        var selection = editor.Selection;
        selection.ApplyPropertyValue(
            TextElement.FontStyleProperty,
            IsItalic(selection) ? FontStyles.Normal : FontStyles.Italic);
    }

    public static void ToggleUnderline(RichTextBox editor)
    {
        editor.Focus();
        var selection = editor.Selection;
        selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            MergeUnderline(selection.GetPropertyValue(Inline.TextDecorationsProperty), enable: !IsUnderline(selection)));
    }

    public static void ToggleStrikethrough(RichTextBox editor)
    {
        editor.Focus();
        var selection = editor.Selection;
        selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            MergeStrike(selection.GetPropertyValue(Inline.TextDecorationsProperty), enable: !IsStrike(selection)));
    }

    public static bool IsBold(TextSelection selection)
    {
        var value = selection.GetPropertyValue(TextElement.FontWeightProperty);
        return value is FontWeight weight &&
               weight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
    }

    public static bool IsItalic(TextSelection selection)
    {
        var value = selection.GetPropertyValue(TextElement.FontStyleProperty);
        return value is FontStyle style && style == FontStyles.Italic;
    }

    public static bool IsUnderline(TextSelection selection)
    {
        var value = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        return HasDecoration(value, TextDecorations.Underline);
    }

    public static bool IsStrike(TextSelection selection)
    {
        var value = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        return HasDecoration(value, TextDecorations.Strikethrough);
    }

    private static bool HasDecoration(object? value, TextDecorationCollection needle)
    {
        if (value is not TextDecorationCollection deco || deco.Count == 0 || needle.Count == 0)
            return false;
        return deco.Contains(needle[0]);
    }

    private static TextDecorationCollection? MergeUnderline(object? current, bool enable)
    {
        var hasStrike = HasDecoration(current, TextDecorations.Strikethrough);
        if (!enable && !hasStrike) return null;
        var result = new TextDecorationCollection();
        if (enable) result.Add(TextDecorations.Underline[0]);
        if (hasStrike) result.Add(TextDecorations.Strikethrough[0]);
        return result;
    }

    private static TextDecorationCollection? MergeStrike(object? current, bool enable)
    {
        var hasUnderline = HasDecoration(current, TextDecorations.Underline);
        if (!enable && !hasUnderline) return null;
        var result = new TextDecorationCollection();
        if (hasUnderline) result.Add(TextDecorations.Underline[0]);
        if (enable) result.Add(TextDecorations.Strikethrough[0]);
        return result;
    }

    public static void ApplyHeading(RichTextBox editor, double fontSize, FontWeight weight)
    {
        editor.Focus();
        if (fontSize <= NormalFontSize && weight == FontWeights.Normal)
        {
            ApplyNormalTypingStyle(editor);
            return;
        }

        var selection = editor.Selection;
        selection.ApplyPropertyValue(TextElement.FontFamilyProperty, NormalFontFamily);
        selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
        selection.ApplyPropertyValue(TextElement.FontWeightProperty, weight);
        selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
        selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
        selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
    }

    public static void ToggleBullets(RichTextBox editor)
    {
        editor.Focus();
        EditingCommands.ToggleBullets.Execute(null, editor);
    }

    public static void ToggleNumbering(RichTextBox editor)
    {
        editor.Focus();
        EditingCommands.ToggleNumbering.Execute(null, editor);
    }

    public static void InsertHyperlink(RichTextBox editor, string url, string? display = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var selection = editor.Selection;
        var label = string.IsNullOrWhiteSpace(display)
            ? LinkTarget.DefaultDisplay(url)
            : display.Trim();

        if (!selection.IsEmpty)
        {
            if (!string.Equals(selection.Text, label, StringComparison.Ordinal))
                selection.Text = label;
        }
        else
        {
            selection.Text = label;
        }

        var link = new Hyperlink(selection.Start, selection.End)
        {
            NavigateUri = uri,
            Cursor = System.Windows.Input.Cursors.IBeam
        };
        if (System.Windows.Application.Current.TryFindResource("LinkBrush") is SolidColorBrush linkBrush)
            link.Foreground = linkBrush;
    }

    public static IReadOnlyList<(TextPointer Start, TextPointer End)> FindAll(
        FlowDocument document, string query)
    {
        var results = new List<(TextPointer Start, TextPointer End)>();
        if (string.IsNullOrEmpty(query) || document is null) return results;

        var chars = CollectChars(document.ContentStart, document.ContentEnd);
        if (chars.Count == 0) return results;

        var haystack = string.Concat(chars.Select(c => c.Ch));
        var startIndex = 0;
        while (startIndex <= haystack.Length - query.Length)
        {
            var index = haystack.IndexOf(query, startIndex, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0) break;
            if (index + query.Length > chars.Count) break;

            var matchStart = chars[index].At;
            var last = chars[index + query.Length - 1].At;
            var matchEnd = last.GetPositionAtOffset(1, LogicalDirection.Forward) ?? last;
            results.Add((matchStart, matchEnd));
            startIndex = index + Math.Max(1, query.Length);
        }

        return results;
    }

    private static List<(char Ch, TextPointer At)> CollectChars(TextPointer start, TextPointer end)
    {
        var chars = new List<(char Ch, TextPointer At)>();
        if (start is null || end is null || start.CompareTo(end) >= 0) return chars;

        var navigator = start;
        while (navigator is not null && navigator.CompareTo(end) < 0)
        {
            if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var run = navigator.GetTextInRun(LogicalDirection.Forward);
                for (var i = 0; i < run.Length; i++)
                {
                    var at = navigator.GetPositionAtOffset(i, LogicalDirection.Forward);
                    if (at is null || at.CompareTo(end) >= 0) break;
                    chars.Add((run[i], at));
                }
            }

            navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
        }

        return chars;
    }
}
