using System.Windows;
using PlainDraft.Services;

namespace PlainDraft;

public partial class App : System.Windows.Application
{
    private SingleInstance? _singleInstance;
    private SettingsService? _settings;
    private ContentService? _content;
    private HotkeyService? _hotkeys;
    private TrayService? _tray;
    private MainWindow? _main;

    static App()
    {
        // TextBox (search): paint selection behind glyphs so Ink stays readable.
        AppContext.SetSwitch(
            "Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering",
            false);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsFirstInstance)
        {
            SingleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }

        _settings = new SettingsService();
        _settings.Load();
        ThemeService.Apply(_settings.Current.ThemeId);
        _content = new ContentService();
        _hotkeys = new HotkeyService();
        _tray = new TrayService();

        var startHidden = HasHiddenStartupArg(e.Args);

        _main = new MainWindow(_settings, _content, _hotkeys);
        // Show first so Loaded creates HWND and registers hotkeys; then hide if requested.
        _main.Show();
        if (startHidden)
            _main.Hide();

        WireTray();
        _tray.UpdateHotkeyLabels(_settings.Current.HotkeyGoToNote, _settings.Current.HotkeyToggleVisible);
        _tray.UpdateVisibilityLabel(_main.IsVisible);

        _singleInstance.ActivatedBySecondInstance += () =>
            Dispatcher.Invoke(() => _main?.GoToNote());
        _singleInstance.StartListening();
    }

    /// <summary>
    /// Start hidden in tray: -minimized / /minimized / -hidden / /hidden
    /// </summary>
    private static bool HasHiddenStartupArg(IReadOnlyList<string> args)
    {
        foreach (var raw in args)
        {
            var a = raw.Trim();
            if (a.Equals("-minimized", StringComparison.OrdinalIgnoreCase)
                || a.Equals("/minimized", StringComparison.OrdinalIgnoreCase)
                || a.Equals("-hidden", StringComparison.OrdinalIgnoreCase)
                || a.Equals("/hidden", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void WireTray()
    {
        if (_tray is null || _main is null || _settings is null) return;

        _tray.GoToNoteRequested += () => Dispatcher.Invoke(() => _main.GoToNote());
        _tray.PrintRequested += () => Dispatcher.Invoke(OpenPrint);
        _tray.ToggleVisibleRequested += () => Dispatcher.Invoke(() =>
        {
            _main.ToggleVisible();
            _tray.UpdateVisibilityLabel(_main.IsVisible);
        });
        _tray.SettingsRequested += () => Dispatcher.Invoke(OpenSettings);
        _tray.AboutRequested += () => Dispatcher.Invoke(OpenAbout);
        _tray.ExitRequested += () => Dispatcher.Invoke(ExitApp);

        _main.IsVisibleChanged += (_, _) =>
            _tray.UpdateVisibilityLabel(_main.IsVisible);
    }

    private void OpenSettings()
    {
        if (_settings is null || _main is null) return;
        var dlg = new SettingsWindow(_settings, _main);
        dlg.Owner = _main;
        if (dlg.ShowDialog() == true)
        {
            _tray?.UpdateHotkeyLabels(_settings.Current.HotkeyGoToNote, _settings.Current.HotkeyToggleVisible);
            _tray?.UpdateVisibilityLabel(_main.IsVisible);
        }
    }

    private void OpenAbout()
    {
        if (_settings is null || _main is null) return;
        var dlg = new AboutWindow { Owner = _main };
        dlg.ShowDialog();
    }

    private void OpenPrint()
    {
        if (_main is null) return;
        _main.OpenPrint();
    }

    public void ExitApp()
    {
        try { _main?.PrepareExit(); } catch { /* ignore */ }
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _singleInstance?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
