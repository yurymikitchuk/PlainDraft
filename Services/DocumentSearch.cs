using System.Windows.Documents;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace PlainDraft.Services;

/// <summary>
/// Indexed find in a RichTextBox — count, current, next/prev (AirPDF-style).
/// </summary>
internal sealed class DocumentSearch
{
    private readonly List<(TextPointer Start, TextPointer End)> _hits = [];
    private int _index = -1;
    private string _query = "";

    public int Count => _hits.Count;
    public int CurrentOneBased => _index < 0 ? 0 : _index + 1;
    public string Query => _query;
    public bool HasQuery => !string.IsNullOrEmpty(_query);

    public void Clear()
    {
        _hits.Clear();
        _index = -1;
        _query = "";
    }

    /// <summary>
    /// Rebuild match list. Optionally select the first hit (Enter / F3 / nav).
    /// Live typing should pass selectFirst: false — only update count.
    /// </summary>
    public bool Rebuild(RichTextBox editor, string query, bool selectFirst = false)
    {
        query = query ?? "";
        _query = query;
        _hits.Clear();
        _index = -1;

        if (string.IsNullOrEmpty(query))
            return false;

        _hits.AddRange(EditorFormatting.FindAll(editor.Document, query));
        if (_hits.Count == 0)
            return false;

        _index = 0;
        if (selectFirst)
            SelectCurrent(editor, focusEditor: false);
        return true;
    }

    public bool Move(RichTextBox editor, int delta, bool focusEditor = false)
    {
        if (_hits.Count == 0)
            return false;

        _index = (_index + delta) % _hits.Count;
        if (_index < 0)
            _index += _hits.Count;
        SelectCurrent(editor, focusEditor);
        return true;
    }

    public void SelectCurrent(RichTextBox editor, bool focusEditor)
    {
        if (_index < 0 || _index >= _hits.Count) return;
        var hit = _hits[_index];
        editor.Selection.Select(hit.Start, hit.End);
        if (focusEditor)
            editor.Focus();
        EditorFormatting.ScrollSelectionIntoView(editor);
    }

    /// <summary>
    /// True if the editor selection already matches the current hit.
    /// </summary>
    public bool HasActiveSelection(RichTextBox editor)
    {
        if (_index < 0 || _index >= _hits.Count) return false;
        var hit = _hits[_index];
        var sel = editor.Selection;
        return sel.Start.CompareTo(hit.Start) == 0 && sel.End.CompareTo(hit.End) == 0;
    }

    public string StatusText()
    {
        if (!HasQuery) return "";
        if (_hits.Count == 0) return "0";
        return $"{CurrentOneBased}/{_hits.Count}";
    }
}
