using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Size = System.Windows.Size;

namespace PlainDraft.Services;

/// <summary>
/// Prints selected FlowDocument pages onto paper with AirPDF-style scale modes.
/// </summary>
internal sealed class NotePrintPaginator : DocumentPaginator
{
    private const double RenderDipScale = 2.0;

    private readonly DocumentPaginator _source;
    private readonly Size _pageSize;
    private readonly IReadOnlyList<int> _pageIndexes;
    private readonly PrintScaleMode _scaleMode;

    public NotePrintPaginator(
        DocumentPaginator source,
        Size pageSize,
        IReadOnlyList<int> pageIndexes,
        PrintScaleMode scaleMode)
    {
        _source = source;
        _pageSize = pageSize;
        _pageIndexes = pageIndexes;
        _scaleMode = scaleMode;
    }

    public override bool IsPageCountValid => true;
    public override int PageCount => _pageIndexes.Count;

    public override Size PageSize
    {
        get => _pageSize;
        set { }
    }

    public override IDocumentPaginatorSource Source => null!;

    public override DocumentPage GetPage(int pageNumber)
    {
        var sourceIndex = _pageIndexes[pageNumber];
        var bitmap = RenderSourcePage(sourceIndex);
        var native = _source.PageSize;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(_pageSize));
            var dest = NotePrintLayout.ComputeContentRect(bitmap, native, _pageSize, _scaleMode);
            dc.PushClip(new RectangleGeometry(new Rect(_pageSize)));
            dc.DrawImage(bitmap, dest);
            dc.Pop();
        }

        return new DocumentPage(visual, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
    }

    /// <summary>
    /// Render directly from the page Visual into a small thumbnail. Do not
    /// allocate the 2× print bitmap used by GetPage.
    /// </summary>
    public BitmapSource RenderPreviewFrame(
        int sourcePageIndex,
        PrintScaleMode scaleMode,
        int pixelWidth,
        int pixelHeight)
    {
        var page = _source.GetPage(sourcePageIndex);
        var sourceSize = page.Size;
        if (sourceSize.Width < 1 || sourceSize.Height < 1)
            sourceSize = _source.PageSize;

        var native = _source.PageSize;
        var bounds = new Rect(0, 0, _pageSize.Width, _pageSize.Height);
        var dest = NotePrintLayout.ComputeContentRect(sourceSize, native, _pageSize, scaleMode);
        pixelWidth = Math.Max(1, pixelWidth);
        pixelHeight = Math.Max(1, pixelHeight);
        var scaleX = pixelWidth / Math.Max(1, _pageSize.Width);
        var scaleY = pixelHeight / Math.Max(1, _pageSize.Height);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scaleX, scaleY));
            dc.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1 / Math.Max(scaleX, scaleY)), bounds);
            dc.PushClip(new RectangleGeometry(bounds));

            var pageBrush = new VisualBrush(page.Visual)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, sourceSize.Width, sourceSize.Height)
            };
            dc.DrawRectangle(pageBrush, null, dest);

            dc.Pop();
            dc.Pop();
        }

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private BitmapSource RenderSourcePage(int sourcePageIndex)
    {
        var page = _source.GetPage(sourcePageIndex);
        var size = page.Size;
        if (size.Width < 1 || size.Height < 1)
            size = _source.PageSize;

        var pixelW = Math.Max(1, (int)Math.Ceiling(size.Width * RenderDipScale));
        var pixelH = Math.Max(1, (int)Math.Ceiling(size.Height * RenderDipScale));

        var rtb = new RenderTargetBitmap(pixelW, pixelH, 96 * RenderDipScale, 96 * RenderDipScale, PixelFormats.Pbgra32);
        rtb.Render(page.Visual);
        rtb.Freeze();
        return rtb;
    }
}
