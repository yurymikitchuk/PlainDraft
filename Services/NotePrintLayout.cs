using System.Windows;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;

namespace PlainDraft.Services;

internal static class NotePrintLayout
{
    public static Rect ComputeContentRect(
        BitmapSource bitmap,
        Size nativeSize,
        Size page,
        PrintScaleMode mode)
    {
        var margin = Math.Min(page.Width, page.Height) * 0.03;
        var availW = Math.Max(1, page.Width - margin * 2);
        var availH = Math.Max(1, page.Height - margin * 2);
        var imageAspect = bitmap.PixelWidth / (double)Math.Max(1, bitmap.PixelHeight);

        double width;
        double height;

        switch (mode)
        {
            case PrintScaleMode.FitWidth:
                width = availW;
                height = width / imageAspect;
                if (height > availH)
                {
                    height = availH;
                    width = height * imageAspect;
                }
                break;

            case PrintScaleMode.FillPage:
                var fill = Math.Max(availW / bitmap.PixelWidth, availH / bitmap.PixelHeight);
                width = bitmap.PixelWidth * fill;
                height = bitmap.PixelHeight * fill;
                break;

            case PrintScaleMode.ActualSize:
                width = Math.Max(1, nativeSize.Width);
                height = Math.Max(1, nativeSize.Height);
                break;

            default:
                var fit = Math.Min(availW / bitmap.PixelWidth, availH / bitmap.PixelHeight);
                width = bitmap.PixelWidth * fit;
                height = bitmap.PixelHeight * fit;
                break;
        }

        return new Rect((page.Width - width) / 2, (page.Height - height) / 2, width, height);
    }

    public static Size NormalizePrintPageSize(Size printable, bool landscape)
    {
        if (landscape && printable.Width < printable.Height)
            return new Size(printable.Height, printable.Width);
        if (!landscape && printable.Width > printable.Height)
            return new Size(printable.Height, printable.Width);
        return printable;
    }
}
