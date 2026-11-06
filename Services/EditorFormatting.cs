using System.Windows;
using System.Windows.Documents;
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
        // One undo step for all property resets (not one per ApplyPropertyValue).
        using (editor.DeclareChangeBlock())
        {
            selection.ApplyPropertyValue(TextElement.FontFamilyProperty, NormalFontFamily);
            selection.ApplyPropertyValue(TextElement.FontSizeProperty, NormalFontSize);
            selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            foreach (var paragraph in EnumerateParagraphsInRange(selection.Start, selection.End))
                ResetParagraphChrome(paragraph);
        }
    }

    public static bool IsHeadingAtCaret(RichTextBox editor)
    {
        // Heading size is usually on the Run (toolbar ApplyPropertyValue), not Paragraph.
        var value = editor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
        if (value is double size)
            return size > NormalFontSize + 0.5;

        return ResolveFontSize(editor.CaretPosition) > NormalFontSize + 0.5;
    }

    /// <summary>
    /// After Enter from a heading: make the new caret paragraph normal text.
    /// </summary>
    public static void ApplyNormalToCaretParagraph(RichTextBox editor)
    {
        var paragraph = FindParagraph(editor.CaretPosition);
        if (paragraph is null) return;

        using (editor.DeclareChangeBlock())
        {
            ResetParagraphChrome(paragraph);
            paragraph.FontFamily = NormalFontFamily;
            paragraph.FontSize = NormalFontSize;
            paragraph.FontWeight = FontWeights.Normal;
            paragraph.FontStyle = FontStyles.Normal;
            foreach (var inline in paragraph.Inlines.ToList())
                NormalizeInlineTree(inline);

            // Empty paragraph / caret typing style (may have no inlines yet).
            var selection = editor.Selection;
            selection.ApplyPropertyValue(TextElement.FontFamilyProperty, NormalFontFamily);
            selection.ApplyPropertyValue(TextElement.FontSizeProperty, NormalFontSize);
            selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
        }
    }

    private static void NormalizeInlineTree(Inline inline)
    {
        inline.FontFamily = NormalFontFamily;
        inline.FontSize = NormalFontSize;
        inline.FontWeight = FontWeights.Normal;
        inline.FontStyle = FontStyles.Normal;
        inline.TextDecorations = null;
        inline.Background = null;

        if (inline is Span span)
        {
            foreach (var child in span.Inlines.ToList())
                NormalizeInlineTree(child);
        }
    }

    public static void ScrollSelectionIntoView(RichTextBox editor)
    {
        var rect = editor.Selection.Start.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty) return;
        editor.ScrollToVerticalOffset(
            Math.Max(0, editor.VerticalOffset + rect.Top - editor.ActualHeight / 3));
    }

    public static bool HasNonWhitespaceText(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        for (var navigator = document.ContentStart;
             navigator is not null && navigator.CompareTo(document.ContentEnd) < 0;
             navigator = navigator.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (navigator.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
                continue;

            var run = navigator.GetTextInRun(LogicalDirection.Forward);
            foreach (var ch in run)
            {
                if (!char.IsWhiteSpace(ch))
                    return true;
            }
        }

        return false;
    }

    public static bool IsDocumentEffectivelyEmpty(FlowDocument document) =>
        !HasNonWhitespaceText(document);

    /// <summary>
    /// After Select-All + Delete, leftover empty paragraphs keep heading formatting.
    /// Reset caret and empty paragraphs to normal text.
    /// </summary>
    public static void EnsureNormalStyleWhenEmpty(
        RichTextBox editor,
        bool alreadyConfirmedEmpty = false)
    {
        if (!alreadyConfirmedEmpty && HasNonWhitespaceText(editor.Document)) return;

        if (editor.Document.Blocks.Count == 0)
        {
            editor.Document.Blocks.Add(CreateNormalParagraph());
        }
        else
        {
            foreach (var block in editor.Document.Blocks)
            {
                if (block is not Paragraph paragraph) continue;
                ResetParagraphChrome(paragraph);
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
            Margin = new Thickness(0),
            FontFamily = NormalFontFamily,
            FontSize = NormalFontSize,
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Normal
        };

    /// <summary>
    /// Clear only vertical Auto space-after/before. Keep Left/Right — lists need
    /// hanging indent (li/fi from RTF) for markers to align.
    /// </summary>
    private static void ResetParagraphChrome(Paragraph paragraph)
    {
        var m = paragraph.Margin;
        paragraph.Margin = new Thickness(m.Left, 0, m.Right, 0);
        var p = paragraph.Padding;
        paragraph.Padding = new Thickness(p.Left, 0, p.Right, 0);
    }

    private static Paragraph? FindParagraph(TextPointer? pointer)
    {
        for (DependencyObject? current = pointer?.Parent;
             current is not null;
             current = LogicalTreeHelper.GetParent(current))
        {
            if (current is Paragraph paragraph)
                return paragraph;
            if (current is FlowDocument)
                break;
        }

        return null;
    }

    private static IEnumerable<Paragraph> EnumerateParagraphsInRange(TextPointer start, TextPointer end)
    {
        var seen = new HashSet<Paragraph>();
        for (var nav = start;
             nav is not null && nav.CompareTo(end) <= 0;
             nav = nav.GetNextContextPosition(LogicalDirection.Forward))
        {
            var paragraph = FindParagraph(nav);
            if (paragraph is not null && seen.Add(paragraph))
                yield return paragraph;
            if (nav.CompareTo(end) == 0) break;
        }

        var endParagraph = FindParagraph(end);
        if (endParagraph is not null && seen.Add(endParagraph))
            yield return endParagraph;
    }

    private static double ResolveFontSize(TextPointer caret)
    {
        // Prefer nearby Inline — toolbar headings set FontSize on Runs, while
        // Paragraph often still reports the document default (14).
        foreach (var direction in new[] { LogicalDirection.Backward, LogicalDirection.Forward })
        {
            if (caret.GetAdjacentElement(direction) is Inline adjacent &&
                !double.IsNaN(adjacent.FontSize) && adjacent.FontSize > 0)
                return adjacent.FontSize;
        }

        if (caret.Parent is Inline inline &&
            !double.IsNaN(inline.FontSize) && inline.FontSize > 0)
            return inline.FontSize;

        var paragraph = FindParagraph(caret);
        if (paragraph is not null && !double.IsNaN(paragraph.FontSize) && paragraph.FontSize > 0)
            return paragraph.FontSize;

        return NormalFontSize;
    }

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
        using (editor.DeclareChangeBlock())
        {
            selection.ApplyPropertyValue(TextElement.FontFamilyProperty, NormalFontFamily);
            selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
            selection.ApplyPropertyValue(TextElement.FontWeightProperty, weight);
            selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            foreach (var paragraph in EnumerateParagraphsInRange(selection.Start, selection.End))
                ResetParagraphChrome(paragraph);
        }
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
        EditorHyperlinks.Initialize(link);
    }
}
