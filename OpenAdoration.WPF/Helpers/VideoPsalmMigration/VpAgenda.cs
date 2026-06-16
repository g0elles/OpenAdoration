using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>The four agenda item kinds OpenAdoration imports from a VideoPsalm <c>.vpagd</c>.</summary>
public enum VpItemType { Song, Scripture, Image, Video }

/// <summary>
/// Per-item flow control from <c>AgendaItemProperties.json</c> (a parallel array, one entry
/// per agenda item in zip order). See VIDEOPSALM_REFERENCE.md §8b.
/// </summary>
public sealed record VpItemProperties(
    int? AutoAdvanceSeconds,
    int VerseOrderIndex,
    IReadOnlyList<int> HiddenSlides)
{
    public static readonly VpItemProperties None = new(null, -1, []);
}

/// <summary>
/// A scripture item as a <b>reference only</b> — verse text is intentionally not carried
/// (it is licensed; resolved at projection time from a legally-installed version).
/// </summary>
public sealed record VpScriptureRef(
    string VersionAbbreviation,
    string VersionName,
    string Language,
    int BookNumber,
    string BookName,
    int Chapter,
    int VerseStart,
    int VerseEnd);

/// <summary>One agenda item, in true agenda (zip central-directory) order.</summary>
public sealed record VpAgendaItem
{
    public required int Index { get; init; }
    public required VpItemType Type { get; init; }
    public required VpItemProperties Properties { get; init; }

    /// <summary>Set for <see cref="VpItemType.Song"/> (null when the source had no usable verses).</summary>
    public Song? Song { get; init; }

    /// <summary>Set for <see cref="VpItemType.Scripture"/>.</summary>
    public VpScriptureRef? Scripture { get; init; }

    /// <summary>
    /// Effective VideoPsalm style for Song/Scripture items (root ← type ← item cascade), mapped to
    /// an OA <c>Theme</c> on import. Null for media items (their bytes fill the screen).
    /// </summary>
    public VpStyle? Style { get; init; }

    /// <summary>Full ZIP entry name of the matched media bytes (Image/Video); null if not found.</summary>
    public string? MediaEntryName { get; init; }

    /// <summary>Caption/name for Image/Video items.</summary>
    public string? MediaCaption { get; init; }
}

/// <summary>An ordered VideoPsalm agenda parsed from a <c>.vpagd</c> archive.</summary>
public sealed record VpAgenda(IReadOnlyList<VpAgendaItem> Items);
