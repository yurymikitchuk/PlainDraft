using System.IO;

namespace PlainDraft.Services;

internal static class AppPaths
{
    public static string DataDirectory { get; } = CreateDataDirectory();

    public static string SettingsJson => Path.Combine(DataDirectory, "settings.json");
    public static string ContentRtf => Path.Combine(DataDirectory, "content.rtf");
    public static string ContentTemplate =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "content_default.rtf");
    public static string TrayIcon =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "PlainDraft_16.png");
    public static string Logo256 =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "PlainDraft_256.png");

    private static string CreateDataDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlainDraft");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
