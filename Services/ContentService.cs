using System.IO;
using System.Windows.Documents;
using DataFormats = System.Windows.DataFormats;

namespace PlainDraft.Services;

public sealed class ContentService
{
    private readonly string _contentPath = AppPaths.ContentRtf;
    private readonly string _templatePath = AppPaths.ContentTemplate;

    public void LoadInto(FlowDocument document)
    {
        EnsureContentFile();
        using var stream = File.OpenRead(_contentPath);
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        range.Load(stream, DataFormats.Rtf);
        EditorFormatting.ClearAllTextBackgrounds(document);
    }

    public void SaveFrom(FlowDocument document)
    {
        using var stream = File.Create(_contentPath);
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        range.Save(stream, DataFormats.Rtf);
    }

    private void EnsureContentFile()
    {
        if (File.Exists(_contentPath)) return;

        if (File.Exists(_templatePath))
        {
            File.Copy(_templatePath, _contentPath);
            return;
        }

        File.WriteAllText(_contentPath, @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs22\par}");
    }
}
