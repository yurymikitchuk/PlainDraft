using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
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

    public BitmapSource RenderPreviewFrame(int sourcePageIndex, PrintScaleMode scaleMode)
    {
        var bitmap = RenderSourcePage(sourcePageIndex);
        var native = _source.PageSize;
        return ComposePreviewFrame(bitmap, native, _pageSize, scaleMode);
    }

    public static BitmapSource ComposePreviewFrame(
        BitmapSource pageBitmap,
        Size nativeSize,
        Size paper,
        PrintScaleMode scaleMode)
    {
        var dest = NotePrintLayout.ComputeContentRect(pageBitmap, nativeSize, paper, scaleMode);

        var bounds = new Rect(0, 0, paper.Width, paper.Height);
        var contentBounds = Rect.Union(bounds, dest);
        contentBounds.Inflate(4, 4);

        var scale = Math.Min(
            (paper.Width + 24) / contentBounds.Width,
            (paper.Height + 24) / contentBounds.Height);
        scale = Math.Min(scale, 1.0);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.PushTransform(new TranslateTransform(-contentBounds.X, -contentBounds.Y));

            dc.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                null,
                contentBounds);
            dc.DrawRectangle(
                Brushes.White,
                new Pen(new SolidColorBrush(Color.FromRgb(0xC2, 0xC2, 0xC2)), 1),
                bounds);
            dc.PushClip(new RectangleGeometry(bounds));
            dc.DrawImage(pageBitmap, dest);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        var pixelW = Math.Max(1, (int)Math.Ceiling(contentBounds.Width * scale));
        var pixelH = Math.Max(1, (int)Math.Ceiling(contentBounds.Height * scale));
        var rtb = new RenderTargetBitmap(pixelW, pixelH, 96, 96, PixelFormats.Pbgra32);
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
