using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PlainDraft.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _goToNoteItem;
    private readonly ToolStripMenuItem _printItem;
    private readonly ToolStripMenuItem _toggleVisibleItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _aboutItem;
    private readonly ToolStripMenuItem _exitItem;

    public event Action? GoToNoteRequested;
    public event Action? PrintRequested;
    public event Action? ToggleVisibleRequested;
    public event Action? SettingsRequested;
    public event Action? AboutRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        Icon icon;
        if (File.Exists(AppPaths.TrayIcon))
        {
            using var bitmap = new Bitmap(AppPaths.TrayIcon);
            icon = Icon.FromHandle(bitmap.GetHicon());
        }
        else
        {
            icon = SystemIcons.Application;
        }

        _goToNoteItem = new ToolStripMenuItem("Перейти к заметке") { ShowShortcutKeys = true };
        _goToNoteItem.Click += (_, _) => GoToNoteRequested?.Invoke();

        _printItem = new ToolStripMenuItem("Печать");
        _printItem.Click += (_, _) => PrintRequested?.Invoke();

        _settingsItem = new ToolStripMenuItem("Настройки");
        _settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        _aboutItem = new ToolStripMenuItem("О программе");
        _aboutItem.Click += (_, _) => AboutRequested?.Invoke();

        _toggleVisibleItem = new ToolStripMenuItem("Скрыть") { ShowShortcutKeys = true };
        _toggleVisibleItem.Click += (_, _) => ToggleVisibleRequested?.Invoke();

        _exitItem = new ToolStripMenuItem("Выход");
        _exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_goToNoteItem);
        menu.Items.Add(_printItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleVisibleItem);
        menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = TruncateTip("Быстрые заметки. Фиксируйте свои идеи в Plain Draft: Quick Notes."),
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => GoToNoteRequested?.Invoke();
    }

    public void UpdateHotkeyLabels(string goToNote, string toggleVisible)
    {
        _goToNoteItem.ShortcutKeyDisplayString = goToNote;
        _toggleVisibleItem.ShortcutKeyDisplayString = toggleVisible;
    }

    public void UpdateVisibilityLabel(bool isWindowVisible)
    {
        _toggleVisibleItem.Text = isWindowVisible ? "Скрыть" : "Показать";
    }

    private static string TruncateTip(string text)
    {
        const int max = 127;
        return text.Length <= max ? text : text[..max];
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
