namespace OpenAdoration.Domain.Common;

/// <summary>
/// Lightweight **bold** markup embedded directly in plain-text song lyrics (F8).
/// No new storage column: SongSection.Lyrics stays plain text; "**word**" marks bold at render time.
/// </summary>
public static class BoldMarkup
{
    private const string Marker = "**";

    public readonly record struct Segment(string Text, bool IsBold);

    /// <summary>Splits text into plain/bold runs for rendering.</summary>
    public static IReadOnlyList<Segment> Split(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var parts = text.Split(Marker);
        var segments = new List<Segment>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) segments.Add(new Segment(parts[i], i % 2 == 1));
        return segments;
    }

    /// <summary>
    /// Toggles ** markers around the selection: wraps it if not already bold, unwraps it if the
    /// selection is already surrounded by markers. An empty selection inserts an empty pair with
    /// the caret left between them, ready to type.
    /// </summary>
    public static (string Text, int SelectionStart, int SelectionLength) ToggleSelection(
        string text, int selectionStart, int selectionLength)
    {
        if (selectionLength == 0)
        {
            var inserted = text.Insert(selectionStart, Marker + Marker);
            return (inserted, selectionStart + Marker.Length, 0);
        }

        var selectionEnd = selectionStart + selectionLength;
        var hasMarkerBefore = selectionStart >= Marker.Length
            && text.Substring(selectionStart - Marker.Length, Marker.Length) == Marker;
        var hasMarkerAfter = selectionEnd + Marker.Length <= text.Length
            && text.Substring(selectionEnd, Marker.Length) == Marker;

        if (hasMarkerBefore && hasMarkerAfter)
        {
            var unwrapped = text.Remove(selectionEnd, Marker.Length)
                                 .Remove(selectionStart - Marker.Length, Marker.Length);
            return (unwrapped, selectionStart - Marker.Length, selectionLength);
        }

        var wrapped = text.Insert(selectionEnd, Marker).Insert(selectionStart, Marker);
        return (wrapped, selectionStart + Marker.Length, selectionLength);
    }
}
