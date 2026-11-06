using System.IO;
using System.Windows.Documents;
using DataFormats = System.Windows.DataFormats;

namespace PlainDraft.Services;

public sealed class ContentService
{
    private readonly string _contentPath = AppPaths.ContentRtf;
    private readonly string _templatePath = AppPaths.ContentTemplate;
    private readonly string _backupPath = AppPaths.ContentRtf + ".bak";

    public string? RecoveryNotice { get; private set; }

    public void LoadInto(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        RecoveryNotice = null;
        EnsureContentFile();

        try
        {
            LoadRtf(document, _contentPath);
        }
        catch (Exception ex) when (IsRecoverableContentError(ex))
        {
            RecoverDocument(document, ex);
        }

    }

    public void SaveFrom(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tempPath = _contentPath + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024,
                       FileOptions.WriteThrough))
            {
                var range = new TextRange(document.ContentStart, document.ContentEnd);
                range.Save(stream, DataFormats.Rtf);
                stream.Flush(flushToDisk: true);
            }

            CommitTempFile(tempPath, _backupPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private void EnsureContentFile()
    {
        if (File.Exists(_contentPath)) return;

        if (File.Exists(_templatePath))
        {
            CopyFileAtomically(_templatePath, _contentPath);
            return;
        }

        var fallback = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs22\par}";
        var tempPath = _contentPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, fallback);
            CommitTempFile(tempPath, backupPath: null);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private void RecoverDocument(FlowDocument document, Exception loadError)
    {
        var quarantined = TryQuarantine(_contentPath, "content.corrupt");

        if (File.Exists(_backupPath))
        {
            try
            {
                LoadRtf(document, _backupPath);

                string? restoreError = null;
                try
                {
                    RestoreBackupToPrimary();
                }
                catch (Exception ex) when (IsRecoverableContentError(ex))
                {
                    restoreError = ex.Message;
                }

                var quarantineInfo = quarantined is null
                    ? ""
                    : $"\nПовреждённый файл сохранён:\n{quarantined}";
                var restoreInfo = restoreError is null
                    ? ""
                    : $"\nНе удалось восстановить основной файл на диске: {restoreError}";
                RecoveryNotice =
                    $"Основной файл заметки повреждён. Загружена резервная копия.{quarantineInfo}{restoreInfo}";
                return;
            }
            catch (Exception backupError) when (IsRecoverableContentError(backupError))
            {
                // Preserve both files and continue with the bundled template.
            }
        }

        try
        {
            if (File.Exists(_templatePath))
                LoadRtf(document, _templatePath);
            else
                ResetDocument(document);
        }
        catch (Exception templateError) when (IsRecoverableContentError(templateError))
        {
            ResetDocument(document);
        }

        string? saveError = null;
        try
        {
            SaveFrom(document);
        }
        catch (Exception ex) when (IsRecoverableContentError(ex))
        {
            saveError = ex.Message;
        }

        var reason = loadError.Message;
        var saveInfo = saveError is null
            ? ""
            : $"\nНе удалось записать восстановленный документ: {saveError}";
        RecoveryNotice = quarantined is null
            ? $"Не удалось прочитать заметку ({reason}). Открыт чистый документ.{saveInfo}"
            : $"Не удалось прочитать заметку ({reason}). Открыт чистый документ.\nПовреждённый файл сохранён:\n{quarantined}{saveInfo}";
    }

    private static void LoadRtf(FlowDocument document, string path)
    {
        ResetDocument(document);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        range.Load(stream, DataFormats.Rtf);
    }

    private static void ResetDocument(FlowDocument document)
    {
        document.Blocks.Clear();
        document.Blocks.Add(new Paragraph(new Run(string.Empty)));
    }

    private void RestoreBackupToPrimary()
    {
        var tempPath = _contentPath + ".restore.tmp";
        try
        {
            CopyFileWithFlush(_backupPath, tempPath);
            var displacedPath = File.Exists(_contentPath)
                ? CreateUniquePath("content.pre-recovery", ".rtf")
                : null;
            CommitTempFile(tempPath, displacedPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void CopyFileAtomically(string sourcePath, string destinationPath)
    {
        var tempPath = destinationPath + ".tmp";
        try
        {
            CopyFileWithFlush(sourcePath, tempPath);
            if (File.Exists(destinationPath))
                File.Replace(tempPath, destinationPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, destinationPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void CopyFileWithFlush(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            FileOptions.WriteThrough);
        source.CopyTo(destination);
        destination.Flush(flushToDisk: true);
    }

    private void CommitTempFile(string tempPath, string? backupPath)
    {
        if (File.Exists(_contentPath))
            File.Replace(tempPath, _contentPath, backupPath, ignoreMetadataErrors: true);
        else
            File.Move(tempPath, _contentPath);
    }

    private string? TryQuarantine(string path, string prefix)
    {
        if (!File.Exists(path)) return null;

        var quarantinePath = CreateUniquePath(prefix, ".rtf");
        try
        {
            File.Move(path, quarantinePath);
            return quarantinePath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string CreateUniquePath(string prefix, string extension)
    {
        var directory = Path.GetDirectoryName(_contentPath) ?? AppPaths.DataDirectory;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(directory, $"{prefix}-{stamp}-{Guid.NewGuid():N}{extension}");
    }

    private static bool IsRecoverableContentError(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or NotSupportedException;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort cleanup; a future save overwrites the same temp path.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup.
        }
    }
}
