using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace PlainDraft.Services;

/// <summary>
/// Applies load-time document cleanup while the shared text index performs its
/// initial walk. This avoids separate passes for spacing, backgrounds, links,
/// theme ink, and the empty-document check.
/// </summary>
internal static class EditorDocumentInitializer
{
    public static bool Initialize(RichTextBox editor, DocumentTextIndex textIndex)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(textIndex);

        var document = editor.Document;
        var ink = System.Windows.Application.Current.TryFindResource("InkBrush") as SolidColorBrush;
        var linkBrush = System.Windows.Application.Current.TryFindResource("LinkBrush") as SolidColorBrush;

        if (ink is not null)
        {
            editor.Foreground = ink;
            editor.CaretBrush = ink;
            document.Foreground = ink;
        }

        textIndex.Rebuild(document, element =>
        {
            if (element is TextElement textElement)
                textElement.Background = null;

            if (element is Paragraph paragraph)
            {
                var margin = paragraph.Margin;
                paragraph.Margin = new Thickness(margin.Left, 0, margin.Right, 0);
                var padding = paragraph.Padding;
                paragraph.Padding = new Thickness(padding.Left, 0, padding.Right, 0);
            }

            if (element is Hyperlink link)
                EditorHyperlinks.Initialize(link, linkBrush);
            else if (ink is not null)
                EditorTheme.ApplyBodyInkToElement(element, ink);
        });

        return textIndex.HasNonWhitespaceText;
    }
}
