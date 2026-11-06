using System.IO;

namespace PlainDraft.Services;

/// <summary>
/// Normalizes web URLs and local/UNC file paths into absolute URIs for Hyperlink.NavigateUri.
/// </summary>
internal static class LinkTarget
{
    public static bool TryNormalize(string? raw, out string absoluteUri)
    {
        absoluteUri = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim().Trim('"');

        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) &&
            IsSupportedScheme(absolute.Scheme))
        {
            absoluteUri = absolute.AbsoluteUri;
            return true;
        }

        if (LooksLikeWindowsPath(text))
        {
            try
            {
                var full = Path.GetFullPath(text);
                absoluteUri = new Uri(full).AbsoluteUri;
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (!text.Contains("://", StringComparison.Ordinal) &&
            !LooksLikeWindowsPath(text))
        {
            var withHttps = "https://" + text;
            if (Uri.TryCreate(withHttps, UriKind.Absolute, out var web) &&
                (web.Scheme == Uri.UriSchemeHttp || web.Scheme == Uri.UriSchemeHttps))
            {
                absoluteUri = web.AbsoluteUri;
                return true;
            }
        }

        return false;
    }

    public static string DefaultDisplay(string absoluteUri)
    {
        if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out var uri))
            return absoluteUri;

        if (uri.IsFile)
        {
            try
            {
                var name = Path.GetFileName(uri.LocalPath);
                return string.IsNullOrEmpty(name) ? uri.LocalPath : name;
            }
            catch
            {
                return uri.LocalPath;
            }
        }

        return absoluteUri;
    }

    private static bool IsSupportedScheme(string scheme) =>
        scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        || scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
        || scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeWindowsPath(string text)
    {
        if (text.StartsWith(@"\\", StringComparison.Ordinal))
            return true;

        if (text.Length >= 3
            && char.IsLetter(text[0])
            && text[1] == ':'
            && (text[2] == '\\' || text[2] == '/'))
            return true;

        return false;
    }
}
