using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PlainDraft.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using RichTextBox = System.Windows.Controls.RichTextBox;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace PlainDraft;

public partial class PrintWindow : Window
{
    private const int MaxPreviewPages = 50;
    private const double PreviewPaperShort = 120;
    private const double PreviewPaperLong = 170;

    private readonly RichTextBox _editor;
    private readonly DispatcherTimer _previewDebounce;

    private DocumentPaginator? _sourcePaginator;
    private Size _pageSize;
    private int _pageCount;
    private int _currentPageIndex;
    private CancellationTokenSource? _previewCts;
    private int _previewVersion;
    private string _lastPreviewKey = "";
    private bool _rangeInitialized;

    public PrintWindow(RichTextBox editor, Window? owner = null)
    {
        Owner = owner;
        _editor = editor;
        InitializeComponent();

        PopulatePrinters();
        PopulateOrientation();
        PopulateScale();

        _previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _previewDebounce.Tick += (_, _) =>
        {
            _previewDebounce.Stop();
            _ = RefreshPreviewAsync();
        };

        Loaded += (_, _) =>
        {
            RebuildSourcePaginator();
            QueuePreviewRefresh(force: true);
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        WindowChromeUtil.ApplyDialogChrome(this);

    private void OnDragMove(object sender, MouseButtonEventArgs e) =>
        WindowChromeUtil.TryDrag(this, e);

    private void PopulatePrinters()
    {
        try
        {
            using var server = new LocalPrintServer();
            var queues = server.GetPrintQueues(
                [EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);

            var list = queues
                .OrderBy(q => q.FullName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            PrinterCombo.ItemsSource = list;

            PrintQueue? preferred = null;
            try { preferred = LocalPrintServer.GetDefaultPrintQueue(); }
            catch { /* no default printer */ }

            if (preferred is not null)
            {
                var match = list.FirstOrDefault(q =>
                    string.Equals(q.FullName, preferred.FullName, StringComparison.OrdinalIgnoreCase));
                PrinterCombo.SelectedItem = match ?? list.FirstOrDefault();
            }
            else
            {
                PrinterCombo.SelectedItem = list.FirstOrDefault();
            }
        }
        catch
        {
            PrinterCombo.ItemsSource = Array.Empty<PrintQueue>();
        }
    }

    private void PopulateOrientation()
    {
        OrientationCombo.Items.Clear();
        OrientationCombo.Items.Add(new ComboBoxItem { Content = "Авто", Tag = PrintOrientationMode.Auto });
        OrientationCombo.Items.Add(new ComboBoxItem { Content = "Книжная", Tag = PrintOrientationMode.Portrait });
        OrientationCombo.Items.Add(new ComboBoxItem { Content = "Альбомная", Tag = PrintOrientationMode.Landscape });
        OrientationCombo.SelectedIndex = 0;
    }

    private void PopulateScale()
    {
        ScaleCombo.Items.Clear();
        ScaleCombo.Items.Add(new ComboBoxItem { Content = "Вписать в страницу", Tag = PrintScaleMode.FitPage });
        ScaleCombo.Items.Add(new ComboBoxItem { Content = "По ширине", Tag = PrintScaleMode.FitWidth });
        ScaleCombo.Items.Add(new ComboBoxItem { Content = "Реальный размер (100%)", Tag = PrintScaleMode.ActualSize });
        ScaleCombo.Items.Add(new ComboBoxItem { Content = "Заполнить страницу", Tag = PrintScaleMode.FillPage });
        ScaleCombo.SelectedIndex = 1;
    }

    private void RebuildSourcePaginator()
    {
        var landscape = NotePrintService.IsLandscape(GetSelectedOrientation());
        if (GetSelectedOrientation() == PrintOrientationMode.Auto)
            landscape = false;

        _pageSize = NotePrintService.ResolvePageSize(PrinterCombo.SelectedItem as PrintQueue, landscape);
        var (_, paginator) = NotePrintService.BuildPaginator(_editor.Document, _pageSize);
        _sourcePaginator = paginator;
        _pageCount = Math.Max(0, paginator.PageCount);
        _currentPageIndex = NotePrintService.CurrentPageIndex(_editor, paginator);

        InfoText.Text = _pageCount <= 0
            ? "В заметке нет страниц для печати."
            : $"В заметке страниц: {_pageCount}. Сейчас открыта: {_currentPageIndex + 1}.";

        if (!_rangeInitialized)
        {
            RangeBox.Text = _pageCount <= 1 ? "1" : $"1-{_pageCount}";
            _rangeInitialized = true;
        }
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (RangeBox is null || RangeFieldBorder is null) return;
        var enable = RangeRadio.IsChecked == true;
        RangeFieldBorder.IsEnabled = enable;
        RangeBox.IsEnabled = enable;
        if (enable)
            RangeBox.Focus();
        QueuePreviewRefresh(force: true);
    }

    private void OnRangeTextChanged(object sender, TextChangedEventArgs e) =>
        QueuePreviewRefresh(force: true);

    private void OnRangeGotFocus(object sender, RoutedEventArgs e) =>
        WindowChromeUtil.SetDialogFieldFocus(this, RangeFieldBorder, focused: true);

    private void OnRangeLostFocus(object sender, RoutedEventArgs e) =>
        WindowChromeUtil.SetDialogFieldFocus(this, RangeFieldBorder, RangeBox.IsKeyboardFocusWithin);

    private void OnPrintOptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RebuildSourcePaginator();
        QueuePreviewRefresh(force: true);
    }

    private void QueuePreviewRefresh(bool force = false)
    {
        if (!IsLoaded) return;
        if (force)
            _lastPreviewKey = "";
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    private async Task RefreshPreviewAsync()
    {
        if (_sourcePaginator is null)
            RebuildSourcePaginator();

        if (!TryResolvePages(out var pages, out var error, forPreview: true))
        {
            PreviewPanel.Children.Clear();
            PreviewStatusText.Text = "";
            _lastPreviewKey = "";
            if (!string.IsNullOrWhiteSpace(error))
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorText.Visibility = Visibility.Collapsed;
            }
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;

        var scaleMode = GetSelectedScale();
        var orientation = GetSelectedOrientation();
        var landscape = orientation == PrintOrientationMode.Landscape;
        var previewKey =
            $"{string.Join(',', pages.Take(MaxPreviewPages))}|{scaleMode}|{orientation}|{_pageSize.Width}x{_pageSize.Height}";
        if (previewKey == _lastPreviewKey)
            return;
        _lastPreviewKey = previewKey;

        var showCount = Math.Min(pages.Count, MaxPreviewPages);
        PreviewStatusText.Text = pages.Count > MaxPreviewPages
            ? $"К печати: {pages.Count} стр. · {GetSelectedOrientationLabel()} · {GetSelectedScaleLabel()}\n(показаны первые {MaxPreviewPages})"
            : $"К печати: {pages.Count} стр. · {GetSelectedOrientationLabel()} · {GetSelectedScaleLabel()}";

        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;
        var version = ++_previewVersion;

        PreviewPanel.Children.Clear();
        for (var i = 0; i < showCount; i++)
            PreviewPanel.Children.Add(CreatePlaceholder(pages[i] + 1, landscape));

        try
        {
            var wrapper = new NotePrintPaginator(_sourcePaginator!, _pageSize, pages, scaleMode);

            for (var i = 0; i < showCount; i++)
            {
                token.ThrowIfCancellationRequested();
                if (version != _previewVersion)
                    return;

                var pageIndex = pages[i];
                var frame = wrapper.RenderPreviewFrame(pageIndex, scaleMode);
                if (i < PreviewPanel.Children.Count && PreviewPanel.Children[i] is Border card)
                    ApplyPreviewImage(card, frame, pageIndex + 1);

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }
        catch (OperationCanceledException)
        {
            // superseded
        }
        catch (Exception ex)
        {
            PreviewStatusText.Text = ex.Message;
            _lastPreviewKey = "";
        }
    }

    private bool TryResolvePages(out List<int> pages, out string? error, bool forPreview)
    {
        pages = [];
        error = null;

        if (_pageCount <= 0)
        {
            if (!forPreview)
                error = "Нет страниц для печати.";
            return false;
        }

        if (AllPagesRadio?.IsChecked == true)
        {
            pages = Enumerable.Range(0, _pageCount).ToList();
            return true;
        }

        if (CurrentPageRadio?.IsChecked == true)
        {
            pages = [Math.Clamp(_currentPageIndex, 0, _pageCount - 1)];
            return true;
        }

        if (RangeRadio?.IsChecked == true)
        {
            if (string.IsNullOrWhiteSpace(RangeBox?.Text))
            {
                if (!forPreview)
                    error = "Укажите диапазон страниц.";
                return false;
            }

            return PageRangeParser.TryParse(RangeBox.Text ?? "", _pageCount, out pages, out error);
        }

        return false;
    }

    private PrintOrientationMode GetSelectedOrientation()
    {
        if (OrientationCombo?.SelectedItem is ComboBoxItem { Tag: PrintOrientationMode mode })
            return mode;
        return PrintOrientationMode.Auto;
    }

    private PrintScaleMode GetSelectedScale()
    {
        if (ScaleCombo?.SelectedItem is ComboBoxItem { Tag: PrintScaleMode mode })
            return mode;
        return PrintScaleMode.FitPage;
    }

    private string GetSelectedOrientationLabel() =>
        OrientationCombo?.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "Авто" : "Авто";

    private string GetSelectedScaleLabel() =>
        ScaleCombo?.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "Вписать" : "Вписать";

    private Border CreatePlaceholder(int pageNumber, bool landscape)
    {
        var width = landscape ? PreviewPaperLong + 16 : PreviewPaperShort + 16;
        var height = landscape ? PreviewPaperShort + 28 : PreviewPaperLong + 28;

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(6, 6, 6, 22)
        };

        var label = new TextBlock
        {
            Text = pageNumber.ToString(),
            FontSize = 11,
            Foreground = TryFindResource("MutedBrush") as Brush ?? Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var grid = new Grid();
        grid.Children.Add(image);
        grid.Children.Add(label);

        return new Border
        {
            Width = width,
            Height = height,
            Margin = new Thickness(4),
            Background = TryFindResource("ChromeBrush") as Brush ?? Brushes.WhiteSmoke,
            BorderBrush = TryFindResource("FieldBorderBrush") as Brush ?? Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid
        };
    }

    private static void ApplyPreviewImage(Border card, BitmapSource bitmap, int pageNumber)
    {
        if (card.Child is not Grid grid)
            return;

        foreach (var child in grid.Children)
        {
            if (child is Image image)
                image.Source = bitmap;
            else if (child is TextBlock label)
                label.Text = pageNumber.ToString();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        if (!TryResolvePages(out var pages, out var error, forPreview: false))
        {
            ErrorText.Text = string.IsNullOrWhiteSpace(error) ? "Не удалось выбрать страницы." : error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (PrinterCombo.SelectedItem is not PrintQueue)
        {
            ErrorText.Text = "Выберите принтер.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            NotePrintService.Print(_editor, new NotePrintSettings
            {
                Pages = pages,
                Printer = (PrintQueue)PrinterCombo.SelectedItem,
                Orientation = GetSelectedOrientation(),
                ScaleMode = GetSelectedScale()
            });
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _previewDebounce.Stop();
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
    }
}
