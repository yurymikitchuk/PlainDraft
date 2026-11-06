using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PlainDraft.Models;
using PlainDraft.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace PlainDraft;

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly ContentService _content;
    private readonly HotkeyService _hotkeys;
    private readonly DocumentSearch _search = new();
    private readonly DocumentTextIndex _textIndex = new();
    private readonly VoiceInputService _voice = new();
    private readonly DispatcherTimer _searchDebounce;
    private bool _suppressClosePrompt;
    private bool _syncingToolbar;
    private bool _resettingEmptyStyle;
    private bool _applyingSearch;
    private bool _syncingVoice;
    private bool _voiceSession;
    private bool _syncingVoiceText;
    private bool _pixelScrollHooked;
    private Run? _voiceRun;
    private bool _dirty;

    private MenuItem? _ctxUndo;
    private MenuItem? _ctxRedo;
    private MenuItem? _ctxCut;
    private MenuItem? _ctxCopy;
    private MenuItem? _ctxPaste;
    private MenuItem? _ctxPastePlain;
    private MenuItem? _ctxSelectAll;
    private MenuItem? _ctxClearAll;
    private MenuItem? _ctxLink;
    private MenuItem? _ctxNormal;

    public MainWindow(SettingsService settings, ContentService content, HotkeyService hotkeys)
    {
        _settings = settings;
        _content = content;
        _hotkeys = hotkeys;
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            RunSearch(selectFirst: false);
        };
        InitializeComponent();
        ApplyGeometry(_settings.Current);
        ThemeService.ThemeChanged += OnThemeChanged;
        _voice.Failed += OnVoiceFailed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        WindowChromeUtil.ApplyPaperChrome(this);

    private void OnThemeChanged() =>
        Dispatcher.Invoke(() =>
        {
            WindowChromeUtil.ApplyPaperChrome(this);
            EditorTheme.ApplyBodyInk(Editor);
        });

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeys.Attach(hwnd);
        _hotkeys.GoToNotePressed += () => Dispatcher.Invoke(GoToNote);
        _hotkeys.ToggleVisiblePressed += () => Dispatcher.Invoke(ToggleVisible);
        _hotkeys.Register(_settings.Current.HotkeyGoToNote, _settings.Current.HotkeyToggleVisible);

        Editor.Document = new FlowDocument
        {
            FontFamily = EditorFormatting.NormalFontFamily,
            FontSize = EditorFormatting.NormalFontSize,
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Normal
        };
        _content.LoadInto(Editor.Document);
        EditorTheme.ApplyDisplayText(Editor);
        if (!EditorDocumentInitializer.Initialize(Editor, _textIndex))
            EditorFormatting.EnsureNormalStyleWhenEmpty(Editor);
        // Load/theme cleanup must not sit on the undo stack — otherwise Undo
        // can revert back to an empty document.
        Editor.IsUndoEnabled = false;
        Editor.IsUndoEnabled = true;
        BuildEditorContextMenu();
        // Prefer TextBoxBase.Undo/Redo — safe no-op when the stack is empty.
        Editor.AddHandler(CommandManager.PreviewExecutedEvent,
            new ExecutedRoutedEventHandler(OnEditorPreviewExecuted), handledEventsToo: true);
        PlaceCaretAtDocumentStart();
        _dirty = false;
        UpdateTrayMinimizeTooltip();
        UpdateMaximizeChrome();
        StateChanged += (_, _) => UpdateMaximizeChrome();

        // WPF focuses the first tab stop (Editor) on Show — clear after layout.
        // Also hook scroll snap after the template ScrollViewer exists.
        Dispatcher.BeginInvoke(() =>
        {
            HookEditorPixelScrollSnap();
            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(this, null);
            if (!string.IsNullOrWhiteSpace(_content.RecoveryNotice))
            {
                System.Windows.MessageBox.Show(
                    this,
                    _content.RecoveryNotice,
                    "Восстановление заметки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }, DispatcherPriority.ContextIdle);
    }

    /// <summary>
    /// Wheel scroll often lands on fractional offsets → ClearType blur on edge
    /// lines. Snap VerticalOffset to whole device pixels.
    /// </summary>
    private void HookEditorPixelScrollSnap()
    {
        if (_pixelScrollHooked) return;
        var scroll = VisualTreeUtil.FindDescendant<ScrollViewer>(Editor);
        if (scroll is null) return;

        _pixelScrollHooked = true;
        scroll.ScrollChanged += (_, e) =>
        {
            if (e.VerticalChange == 0) return;
            var snapped = Math.Round(scroll.VerticalOffset);
            if (Math.Abs(scroll.VerticalOffset - snapped) < 0.01) return;
            scroll.ScrollToVerticalOffset(snapped);
        };
    }

    private void OnEditorPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Undo)
        {
            e.Handled = true;
            try { Editor.Undo(); } catch { /* ignore */ }
            return;
        }

        if (e.Command == ApplicationCommands.Redo)
        {
            e.Handled = true;
            try { Editor.Redo(); } catch { /* ignore */ }
        }
    }

    private void PlaceCaretAtDocumentStart()
    {
        var start = Editor.Document.ContentStart;
        Editor.CaretPosition = start;
        Editor.Selection.Select(start, start);
        Editor.ScrollToHome();
    }

    private void ApplyGeometry(AppSettings settings)
    {
        Width = settings.Window.Width;
        Height = settings.Window.Height;
        if (settings.Window.X is double x && settings.Window.Y is double y)
        {
            Left = x;
            Top = y;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void RebindHotkeys()
    {
        _hotkeys.Register(_settings.Current.HotkeyGoToNote, _settings.Current.HotkeyToggleVisible);
        UpdateTrayMinimizeTooltip();
    }

    private void UpdateTrayMinimizeTooltip() =>
        TrayMinimizeButton.ToolTip = $"Скрыть ({_settings.Current.HotkeyToggleVisible})";

    public void GoToNote()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Editor.Focus();
    }

    public void OpenPrint()
    {
        SaveIfDirty();
        var dlg = new PrintWindow(Editor, this);
        dlg.ShowDialog();
    }

    public void ToggleVisible()
    {
        if (IsVisible)
        {
            SaveIfDirty();
            Hide();
        }
        else GoToNote();
    }

    private void OnTrayMinimizeClick(object sender, RoutedEventArgs e) =>
        ToggleVisible();

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void UpdateMaximizeChrome()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.ToolTip = maximized ? "Свернуть в окно" : "Развернуть";
        // Inward corners when maximized; outward when normal.
        MaximizeButtonIcon.Data = Geometry.Parse(maximized
            ? "M10,4H14V6H12V8H10V4M4,10H6V12H8V14H4V10M16,10H20V14H18V12H16V10M10,16H12V18H14V20H10V16Z"
            : "M4,4H10V6H6V10H4V4M14,4H20V10H18V6H14V4M4,14H6V18H10V20H4V14M18,14H20V20H14V18H18V14Z");
    }

    public void PrepareExit()
    {
        if (_voiceSession) EndVoiceSession(dismissUi: true);
        Persist();

        ThemeService.ThemeChanged -= OnThemeChanged;
        _voice.Dispose();
        _suppressClosePrompt = true;
        Hide();
    }

    private void SaveIfDirty()
    {
        if (!_dirty) return;
        _content.SaveFrom(Editor.Document);
        _dirty = false;
    }

    private void Persist()
    {
        SaveIfDirty();
        _settings.Current.Window.Width = Width;
        _settings.Current.Window.Height = Height;
        _settings.Current.Window.X = Left;
        _settings.Current.Window.Y = Top;
        _settings.Save();
    }

    private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SaveIfDirty();

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_suppressClosePrompt) return;
        if (System.Windows.Application.Current is App app)
        {
            e.Cancel = true;
            app.ExitApp();
        }
    }

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        WindowChromeUtil.TryDrag(this, e, excludeEditor: true);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            EditorHyperlinks.UpdateCursor(Editor, Mouse.DirectlyOver as DependencyObject);

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F3)
        {
            EnsureSearchAndMove(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1, focusEditor: true);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.X)
        {
            EditorFormatting.ToggleStrikethrough(Editor);
            SyncFormatToggles();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.B:
                    EditorFormatting.ToggleBold(Editor);
                    SyncFormatToggles();
                    e.Handled = true;
                    return;
                case Key.I:
                    EditorFormatting.ToggleItalic(Editor);
                    SyncFormatToggles();
                    e.Handled = true;
                    return;
                case Key.U:
                    EditorFormatting.ToggleUnderline(Editor);
                    SyncFormatToggles();
                    e.Handled = true;
                    return;
                case Key.K:
                    InsertLinkFromUi();
                    e.Handled = true;
                    return;
            }
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            EditorHyperlinks.UpdateCursor(Editor, Mouse.DirectlyOver as DependencyObject);
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        SyncFormatToggles();
    }

    private void SyncFormatToggles()
    {
        if (_syncingToolbar) return;
        _syncingToolbar = true;
        try
        {
            var selection = Editor.Selection;
            BtnBold.IsChecked = EditorFormatting.IsBold(selection);
            BtnItalic.IsChecked = EditorFormatting.IsItalic(selection);
            BtnUnderline.IsChecked = EditorFormatting.IsUnderline(selection);
            BtnStrike.IsChecked = EditorFormatting.IsStrike(selection);
        }
        finally
        {
            _syncingToolbar = false;
        }
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_resettingEmptyStyle) return;

        _textIndex.Invalidate();
        _search.InvalidateHits();
        _dirty = true;

        if (!_applyingSearch && _search.HasQuery)
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        // Adding text cannot make a non-empty document empty. Only scan after
        // deletion/replacement, and stop at the first non-whitespace run.
        if (!e.Changes.Any(change => change.RemovedLength > 0)) return;
        if (EditorFormatting.HasNonWhitespaceText(Editor.Document)) return;

        _resettingEmptyStyle = true;
        try
        {
            EditorFormatting.EnsureNormalStyleWhenEmpty(Editor, alreadyConfirmedEmpty: true);
            SyncFormatToggles();
        }
        finally
        {
            _resettingEmptyStyle = false;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchChromeVisibility();
        if (string.IsNullOrEmpty(SearchBox.Text))
        {
            _searchDebounce.Stop();
            ClearSearch();
            return;
        }

        UpdateSearchUi();
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ClearSearchBox();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter) return;
        e.Handled = true;
        EnsureSearchAndMove(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1, focusEditor: true);
    }

    private void OnSearchClearClick(object sender, RoutedEventArgs e) => ClearSearchBox();

    private void OnSearchPrevClick(object sender, RoutedEventArgs e) =>
        EnsureSearchAndMove(-1, focusEditor: true);

    private void OnSearchNextClick(object sender, RoutedEventArgs e) =>
        EnsureSearchAndMove(1, focusEditor: true);

    private void ClearSearchBox()
    {
        _searchDebounce.Stop();
        SearchBox.Text = "";
        ClearSearch();
        UpdateSearchChromeVisibility();
        SearchBox.Focus();
    }

    private void ClearSearch()
    {
        _search.Clear();
        UpdateSearchUi();
    }

    private void RunSearch(bool selectFirst)
    {
        var query = SearchBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            ClearSearch();
            return;
        }

        _applyingSearch = true;
        try
        {
            _search.Rebuild(Editor, _textIndex, query, selectFirst);
            UpdateSearchUi();
        }
        finally
        {
            _applyingSearch = false;
        }
    }

    private void EnsureSearchAndMove(int delta, bool focusEditor)
    {
        var query = SearchBox.Text;
        if (string.IsNullOrEmpty(query)) return;

        if (_search.Count == 0 ||
            !string.Equals(_search.Query, query, StringComparison.Ordinal))
        {
            RunSearch(selectFirst: true);
            if (_search.Count > 0)
                _search.SelectCurrent(Editor, focusEditor);
            return;
        }

        // After live recount, first nav selects current hit instead of skipping it.
        if (!_search.HasActiveSelection(Editor))
        {
            _applyingSearch = true;
            try
            {
                _search.SelectCurrent(Editor, focusEditor);
                UpdateSearchUi();
            }
            finally
            {
                _applyingSearch = false;
            }
            return;
        }

        _applyingSearch = true;
        try
        {
            _search.Move(Editor, delta, focusEditor);
            UpdateSearchUi();
        }
        finally
        {
            _applyingSearch = false;
        }
    }

    private void UpdateSearchChromeVisibility()
    {
        var empty = string.IsNullOrEmpty(SearchBox.Text);
        var chrome = empty ? Visibility.Collapsed : Visibility.Visible;
        SearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        SearchStatusText.Visibility = chrome;
        SearchChromeSeparator.Visibility = chrome;
        SearchNextButton.Visibility = chrome;
        SearchPrevButton.Visibility = chrome;
        SearchClearButton.Visibility = chrome;
    }

    private void UpdateSearchUi()
    {
        var hasQuery = !string.IsNullOrEmpty(SearchBox.Text);
        SearchPrevButton.IsEnabled = hasQuery;
        SearchNextButton.IsEnabled = hasQuery;
        SearchStatusText.Text = hasQuery ? _search.StatusText() : "";
        UpdateSearchChromeVisibility();
    }

    private void OnTagsClick(object sender, RoutedEventArgs e)
    {
        var menu = ToolbarMenus.Create(this, scrollable: true);
        var tags = DocumentTags.CollectUniqueSorted(_textIndex, Editor.Document);
        if (tags.Count == 0)
        {
            var empty = ToolbarMenus.Item(this, "Меток нет", static () => { });
            empty.IsEnabled = false;
            menu.Items.Add(empty);
        }
        else
        {
            foreach (var tag in tags)
                menu.Items.Add(ToolbarMenus.Item(this, tag.Name, () => DocumentTags.JumpTo(Editor, tag)));
        }

        ToolbarMenus.Open(menu, (UIElement)sender, PlacementMode.Bottom);
    }

    private void OnHeadingClick(object sender, RoutedEventArgs e)
    {
        var menu = ToolbarMenus.Create(this);
        menu.Items.Add(ToolbarMenus.HeadingItem(this, "Заголовок 1", 22, FontWeights.Normal,
            () => EditorFormatting.ApplyHeading(Editor, 22, FontWeights.Normal)));
        menu.Items.Add(ToolbarMenus.HeadingItem(this, "Заголовок 2", 18, FontWeights.Normal,
            () => EditorFormatting.ApplyHeading(Editor, 18, FontWeights.Normal)));
        menu.Items.Add(ToolbarMenus.HeadingItem(this, "Обычный текст", 14, FontWeights.Normal,
            () => EditorFormatting.ApplyNormalTypingStyle(Editor)));
        ToolbarMenus.Open(menu, (UIElement)sender);
    }

    private void OnListClick(object sender, RoutedEventArgs e)
    {
        var menu = ToolbarMenus.Create(this);
        menu.Items.Add(ToolbarMenus.Item(this, "Маркированный", () => EditorFormatting.ToggleBullets(Editor)));
        menu.Items.Add(ToolbarMenus.Item(this, "Нумерованный", () => EditorFormatting.ToggleNumbering(Editor)));
        ToolbarMenus.Open(menu, (UIElement)sender);
    }

    private void OnBoldClick(object sender, RoutedEventArgs e)
    {
        if (_syncingToolbar) return;
        EditorFormatting.ToggleBold(Editor);
        SyncFormatToggles();
    }

    private void OnItalicClick(object sender, RoutedEventArgs e)
    {
        if (_syncingToolbar) return;
        EditorFormatting.ToggleItalic(Editor);
        SyncFormatToggles();
    }

    private void OnUnderlineClick(object sender, RoutedEventArgs e)
    {
        if (_syncingToolbar) return;
        EditorFormatting.ToggleUnderline(Editor);
        SyncFormatToggles();
    }

    private void OnStrikeClick(object sender, RoutedEventArgs e)
    {
        if (_syncingToolbar) return;
        EditorFormatting.ToggleStrikethrough(Editor);
        SyncFormatToggles();
    }

    private void OnLinkClick(object sender, RoutedEventArgs e) => InsertLinkFromUi();

    private void OnVoiceChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingVoice) return;
        BeginVoiceSession();
    }

    private void OnVoiceUnchecked(object sender, RoutedEventArgs e)
    {
        if (_syncingVoice) return;
        EndVoiceSession(dismissUi: true);
    }

    /// <summary>
    /// Dictate into a TextBox (TSF-safe), mirror into a dedicated Run so TSF
    /// corrections can never consume adjacent FlowDocument content.
    /// </summary>
    private void BeginVoiceSession()
    {
        var selection = Editor.Selection;
        if (!selection.IsEmpty)
            selection.Text = string.Empty;

        var insertion = selection.Start.GetInsertionPosition(LogicalDirection.Forward);
        _voiceRun = new Run(string.Empty, insertion);

        _syncingVoiceText = true;
        try { VoiceCaptureBox.Clear(); }
        finally { _syncingVoiceText = false; }

        _voiceSession = true;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible || !_voiceSession) return;
            VoiceCaptureBox.Focus();
            _voice.Start();
        }, DispatcherPriority.Input);
    }

    private void EndVoiceSession(bool dismissUi)
    {
        if (!_voiceSession) return;
        _voiceSession = false;

        if (dismissUi)
            _voice.Stop();

        var run = _voiceRun;
        _voiceRun = null;

        if (run?.Parent is not null)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                run.SiblingInlines?.Remove(run);
            }
            else
            {
                var end = run.ContentEnd.GetInsertionPosition(LogicalDirection.Forward);
                Editor.CaretPosition = end;
                Editor.Selection.Select(end, end);
            }
        }

        Editor.Focus();
    }

    private void OnVoiceCaptureTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_voiceSession || _syncingVoiceText || _voiceRun?.Parent is null) return;

        _voiceRun.Text = VoiceCaptureBox.Text;
        _dirty = true;
    }

    private void OnVoiceFailed(string message)
    {
        Dispatcher.Invoke(() =>
        {
            EndVoiceSession(dismissUi: false);
            SetVoiceToggle(false);
            System.Windows.MessageBox.Show(
                this,
                message,
                "Голосовой ввод",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    private void SetVoiceToggle(bool isOn)
    {
        _syncingVoice = true;
        try { BtnVoice.IsChecked = isOn; }
        finally { _syncingVoice = false; }
    }

    private void BuildEditorContextMenu()
    {
        var menu = ToolbarMenus.Create(this);

        _ctxUndo = ToolbarMenus.Item(this, "Отменить", () => { try { Editor.Undo(); } catch { /* ignore */ } });
        _ctxRedo = ToolbarMenus.Item(this, "Повторить", () => { try { Editor.Redo(); } catch { /* ignore */ } });
        _ctxCut = ToolbarMenus.Item(this, "Вырезать", () => ApplicationCommands.Cut.Execute(null, Editor));
        _ctxCopy = ToolbarMenus.Item(this, "Копировать", () => ApplicationCommands.Copy.Execute(null, Editor));
        _ctxPaste = ToolbarMenus.Item(this, "Вставить", () => ApplicationCommands.Paste.Execute(null, Editor));
        _ctxPastePlain = ToolbarMenus.Item(this, "Вставить без форматирования", PastePlainText);
        _ctxSelectAll = ToolbarMenus.Item(this, "Выделить всё", () => ApplicationCommands.SelectAll.Execute(null, Editor));
        _ctxClearAll = ToolbarMenus.Item(this, "Очистить всё", ClearAllContent);
        _ctxLink = ToolbarMenus.Item(this, "Вставить ссылку…", InsertLinkFromUi);
        _ctxNormal = ToolbarMenus.Item(this, "Обычный текст", () =>
        {
            EditorFormatting.ApplyNormalTypingStyle(Editor);
            SyncFormatToggles();
        });

        menu.Items.Add(_ctxUndo);
        menu.Items.Add(_ctxRedo);
        menu.Items.Add(new Separator());
        menu.Items.Add(_ctxCut);
        menu.Items.Add(_ctxCopy);
        menu.Items.Add(_ctxPaste);
        menu.Items.Add(_ctxPastePlain);
        menu.Items.Add(new Separator());
        menu.Items.Add(_ctxSelectAll);
        menu.Items.Add(_ctxClearAll);
        menu.Items.Add(new Separator());
        menu.Items.Add(_ctxLink);
        menu.Items.Add(_ctxNormal);

        // Align visible face to cursor (cancel template shadow inset on top/left).
        var inset = ToolbarMenus.MacShadowPopupInset();
        menu.HorizontalOffset = -inset;
        menu.VerticalOffset = -(inset - 4);
        Editor.ContextMenu = menu;
    }

    private void OnEditorContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var hasSelection = !Editor.Selection.IsEmpty;
        var hasContent = !EditorFormatting.IsDocumentEffectivelyEmpty(Editor.Document);
        var canPaste = System.Windows.Clipboard.ContainsText() ||
                       System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Rtf) ||
                       System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.UnicodeText);

        if (_ctxUndo is not null) _ctxUndo.IsEnabled = Editor.CanUndo;
        if (_ctxRedo is not null) _ctxRedo.IsEnabled = Editor.CanRedo;
        if (_ctxCut is not null) _ctxCut.IsEnabled = hasSelection;
        if (_ctxCopy is not null) _ctxCopy.IsEnabled = hasSelection;
        if (_ctxPaste is not null) _ctxPaste.IsEnabled = canPaste;
        if (_ctxPastePlain is not null) _ctxPastePlain.IsEnabled = System.Windows.Clipboard.ContainsText();
        if (_ctxSelectAll is not null) _ctxSelectAll.IsEnabled = hasContent;
        if (_ctxClearAll is not null) _ctxClearAll.IsEnabled = hasContent;
        if (_ctxNormal is not null) _ctxNormal.IsEnabled = hasSelection || hasContent;
    }

    private void PastePlainText()
    {
        if (!System.Windows.Clipboard.ContainsText()) return;
        Editor.Selection.Text = System.Windows.Clipboard.GetText();
        EditorFormatting.ApplyNormalTypingStyle(Editor);
        Editor.Focus();
    }

    private void ClearAllContent()
    {
        Editor.SelectAll();
        Editor.Selection.Text = string.Empty;
        EditorFormatting.EnsureNormalStyleWhenEmpty(Editor);
        SyncFormatToggles();
        Editor.Focus();
    }

    private void InsertLinkFromUi()
    {
        var selected = Editor.Selection.IsEmpty ? null : Editor.Selection.Text;
        var dlg = new LinkWindow(selected, this);
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Url)) return;

        EditorFormatting.InsertHyperlink(Editor, dlg.Url!, dlg.DisplayText);
        Editor.Focus();
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Plain Enter after a heading → next paragraph becomes «Обычный текст».
        // Shift+Enter stays a LineBreak and keeps current style.
        if (e.Key != Key.Enter) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (!EditorFormatting.IsHeadingAtCaret(Editor)) return;

        // Handle Enter ourselves so Normal is applied after the break, not raced
        // via BeginInvoke (default Enter copies heading size onto the new para).
        e.Handled = true;
        EditingCommands.EnterParagraphBreak.Execute(null, Editor);
        EditorFormatting.ApplyNormalToCaretParagraph(Editor);
    }

    private void OnEditorPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        EditorHyperlinks.TryOpenOnCtrlClick(Editor, e);

    private void OnEditorPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) =>
        EditorHyperlinks.UpdateCursor(Editor, e.OriginalSource as DependencyObject);

    private void OnEditorMouseLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
        EditorHyperlinks.ClearCursorOverride();
}
