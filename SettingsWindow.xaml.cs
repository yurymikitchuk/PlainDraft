using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlainDraft.Models;
using PlainDraft.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace PlainDraft;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly MainWindow _main;
    private readonly string _savedThemeId;
    private bool _ready;
    private bool _applied;
    private bool _suppressHotkeyFocus;
    private bool _capturingGoTo;
    private bool _capturingToggle;
    private string _goToNote;
    private string _toggleVisible;
    private string _themeId;

    public SettingsWindow(SettingsService settings, MainWindow main)
    {
        _settings = settings;
        _main = main;
        _goToNote = settings.Current.HotkeyGoToNote;
        _toggleVisible = settings.Current.HotkeyToggleVisible;
        _savedThemeId = ThemeService.NormalizeId(settings.Current.ThemeId);
        _themeId = _savedThemeId;
        InitializeComponent();

        GoToNoteBox.Text = _goToNote;
        ToggleBox.Text = _toggleVisible;

        if (_themeId == ThemeService.ChalkboardId)
            ThemeChalkboardRadio.IsChecked = true;
        else
            ThemePaperRadio.IsChecked = true;

        _ready = true;
        Closed += OnClosed;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        if (ThemeChalkboardRadio.IsChecked == true)
            _themeId = ThemeService.ChalkboardId;
        else if (ThemePaperRadio.IsChecked == true)
            _themeId = ThemeService.PaperId;
        else
            return;

        ThemeService.Apply(_themeId);
        WindowChromeUtil.ApplyDialogChrome(this);
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        WindowChromeUtil.ApplyDialogChrome(this);

    private void OnDragMove(object sender, MouseButtonEventArgs e) =>
        WindowChromeUtil.TryDrag(this, e);

    private void OnGoToNoteFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WindowChromeUtil.SetDialogFieldFocus(this, GoToNoteFieldBorder, focused: true);
        if (_suppressHotkeyFocus) return;
        _capturingGoTo = true;
        GoToNoteBox.Text = "ожидание…";
    }

    private void OnToggleFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WindowChromeUtil.SetDialogFieldFocus(this, ToggleFieldBorder, focused: true);
        if (_suppressHotkeyFocus) return;
        _capturingToggle = true;
        ToggleBox.Text = "ожидание…";
    }

    private void OnGoToNoteLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WindowChromeUtil.SetDialogFieldFocus(this, GoToNoteFieldBorder, GoToNoteBox.IsKeyboardFocusWithin);
        if (!_capturingGoTo) return;
        GoToNoteBox.Text = _goToNote;
        _capturingGoTo = false;
    }

    private void OnToggleLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WindowChromeUtil.SetDialogFieldFocus(this, ToggleFieldBorder, ToggleBox.IsKeyboardFocusWithin);
        if (!_capturingToggle) return;
        ToggleBox.Text = _toggleVisible;
        _capturingToggle = false;
    }

    private void OnGoToNoteKeyDown(object sender, KeyEventArgs e) =>
        CaptureHotkey(e, ref _capturingGoTo, GoToNoteBox, ref _goToNote);

    private void OnToggleKeyDown(object sender, KeyEventArgs e) =>
        CaptureHotkey(e, ref _capturingToggle, ToggleBox, ref _toggleVisible);

    private static void CaptureHotkey(
        KeyEventArgs e,
        ref bool capturing,
        TextBox box,
        ref string stored)
    {
        if (!capturing) return;
        e.Handled = true;

        if (e.Key is Key.Escape)
        {
            box.Text = stored;
            capturing = false;
            Keyboard.ClearFocus();
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            return;

        var gesture = HotkeyService.Normalize(Keyboard.Modifiers, key);
        if (!HotkeyService.TryParse(gesture, out _, out _)) return;

        stored = gesture;
        box.Text = gesture;
        capturing = false;
        Keyboard.ClearFocus();
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        _suppressHotkeyFocus = true;
        _capturingGoTo = false;
        _capturingToggle = false;
        _goToNote = AppSettings.DefaultHotkeyGoToNote;
        _toggleVisible = AppSettings.DefaultHotkeyToggleVisible;
        GoToNoteBox.Text = _goToNote;
        ToggleBox.Text = _toggleVisible;

        _themeId = ThemeService.PaperId;
        _ready = false;
        ThemePaperRadio.IsChecked = true;
        ThemeChalkboardRadio.IsChecked = false;
        _ready = true;
        ThemeService.Apply(_themeId);
        WindowChromeUtil.ApplyDialogChrome(this);

        Keyboard.ClearFocus();
        Dispatcher.BeginInvoke(() => _suppressHotkeyFocus = false);
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (_goToNote == _toggleVisible)
        {
            System.Windows.MessageBox.Show(this, "Горячие клавиши не должны совпадать.", "Настройки",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _themeId = ThemeService.NormalizeId(_themeId);

        _settings.Current.HotkeyGoToNote = _goToNote;
        _settings.Current.HotkeyToggleVisible = _toggleVisible;
        _settings.Current.ThemeId = _themeId;
        _settings.Save();
        _main.RebindHotkeys();
        ThemeService.Apply(_themeId);
        _applied = true;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (!_applied && _themeId != _savedThemeId)
            ThemeService.Apply(_savedThemeId);
    }
}
