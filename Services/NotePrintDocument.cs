using System.IO;
using System.Windows;
using System.Windows.Documents;
using Size = System.Windows.Size;

namespace PlainDraft.Services;

internal static class NotePrintDocument
{
    public static FlowDocument Clone(FlowDocument source)
    {
        var range = new TextRange(source.ContentStart, source.ContentEnd);
        using var ms = new MemoryStream();
        range.Save(ms, System.Windows.DataFormats.XamlPackage);
        ms.Position = 0;

        var copy = new FlowDocument();
        var dest = new TextRange(copy.ContentStart, copy.ContentEnd);
        dest.Load(ms, System.Windows.DataFormats.XamlPackage);

        copy.FontFamily = source.FontFamily;
        copy.FontSize = source.FontSize;
        copy.FontWeight = source.FontWeight;
        copy.FontStyle = source.FontStyle;
        copy.Foreground = source.Foreground;
        copy.Background = source.Background;
        copy.TextAlignment = source.TextAlignment;
        copy.LineHeight = source.LineHeight;
        return copy;
    }

    public static void ConfigurePage(FlowDocument doc, Size pageSize)
    {
        var pad = Math.Min(pageSize.Width, pageSize.Height) * 0.06;
        pad = Math.Clamp(pad, 36, 72);
        doc.PageWidth = pageSize.Width;
        doc.PageHeight = pageSize.Height;
        doc.PagePadding = new Thickness(pad);
        doc.ColumnGap = 0;
        doc.ColumnWidth = Math.Max(48, pageSize.Width - pad * 2);
    }

    public static DocumentPaginator GetPaginator(FlowDocument doc)
    {
        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.ComputePageCount();
        return paginator;
    }

    public static int FindCaretPageIndex(FlowDocument liveDoc, TextPointer caret, DocumentPaginator printPaginator)
    {
        if (printPaginator.PageCount <= 0) return 0;

        try
        {
            // Map caret offset from the live document onto the print clone proportionally.
            var liveLen = Math.Max(1, new TextRange(liveDoc.ContentStart, liveDoc.ContentEnd).Text.Length);
            var caretOffset = Math.Clamp(
                new TextRange(liveDoc.ContentStart, caret).Text.Length,
                0,
                liveLen);
            var ratio = caretOffset / (double)liveLen;
            return Math.Clamp((int)(ratio * printPaginator.PageCount), 0, printPaginator.PageCount - 1);
        }
        catch
        {
            return 0;
        }
    }
}
