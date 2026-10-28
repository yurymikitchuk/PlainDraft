using System.Printing;
using System.Windows;
using System.Windows.Documents;
using PrintDialog = System.Windows.Controls.PrintDialog;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Size = System.Windows.Size;

namespace PlainDraft.Services;

internal static class NotePrintService
{
    public static Size ResolvePageSize(PrintQueue? queue, bool landscape)
    {
        try
        {
            var dialog = new PrintDialog();
            if (queue is not null)
                dialog.PrintQueue = queue;

            var ticket = dialog.PrintTicket ?? new PrintTicket();
            ticket.PageOrientation = landscape
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;
            dialog.PrintTicket = ticket;

            var printable = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            if (printable.Width < 32 || printable.Height < 32)
                printable = landscape ? new Size(1056, 816) : new Size(816, 1056);

            return NotePrintLayout.NormalizePrintPageSize(printable, landscape);
        }
        catch
        {
            return landscape ? new Size(1056, 816) : new Size(816, 1056);
        }
    }

    public static bool IsLandscape(PrintOrientationMode mode) =>
        mode == PrintOrientationMode.Landscape;

    public static (FlowDocument Doc, DocumentPaginator Paginator) BuildPaginator(
        FlowDocument source,
        Size pageSize)
    {
        var copy = NotePrintDocument.Clone(source);
        NotePrintDocument.ConfigurePage(copy, pageSize);
        var paginator = NotePrintDocument.GetPaginator(copy);
        return (copy, paginator);
    }

    public static int CurrentPageIndex(RichTextBox editor, DocumentPaginator printPaginator) =>
        NotePrintDocument.FindCaretPageIndex(editor.Document, editor.CaretPosition, printPaginator);

    public static void Print(RichTextBox editor, NotePrintSettings settings)
    {
        if (settings.Pages.Count == 0) return;

        var landscape = IsLandscape(settings.Orientation);
        // Auto → portrait for notes.
        if (settings.Orientation == PrintOrientationMode.Auto)
            landscape = false;

        var dialog = new PrintDialog();
        if (settings.Printer is not null)
            dialog.PrintQueue = settings.Printer;

        var ticket = dialog.PrintTicket ?? new PrintTicket();
        ticket.PageOrientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        dialog.PrintTicket = ticket;

        var pageSize = ResolvePageSize(settings.Printer, landscape);
        var (_, sourcePaginator) = BuildPaginator(editor.Document, pageSize);
        var paginator = new NotePrintPaginator(
            sourcePaginator,
            pageSize,
            settings.Pages,
            settings.ScaleMode);

        dialog.PrintDocument(paginator, "Plain Draft");
    }
}
