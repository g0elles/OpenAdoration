namespace OpenAdoration.Application.Common;

/// <summary>
/// The atomic unit the projection engine displays.
/// Generated at runtime from domain entities — never stored in the database.
/// </summary>
public sealed class Slide
{
    public string Content { get; }
    public SlideType Type { get; }

    /// <summary>
    /// Human-readable label shown to the operator (e.g. "Verse 1", "John 3:16", "background.jpg").
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Path to the media file. Only set when Type is <see cref="SlideType.Media"/>.
    /// </summary>
    public string? MediaPath { get; }

    /// <summary>
    /// Optional theme override for this specific slide.
    /// When null, the active service-level theme applies.
    /// </summary>
    public int? ThemeId { get; }

    /// <summary>
    /// Metadata for header/footer token resolution (e.g. [SongTitle], [BibleBookName]).
    /// Never null — defaults to <see cref="SlideContext.Empty"/> when not supplied.
    /// </summary>
    public SlideContext Context { get; }

    /// <summary>
    /// Ad-hoc per-session rendering override (F7: Stage View quick style fix). Null means the
    /// resolved theme applies unmodified. See <see cref="SlideStyleOverride"/>.
    /// </summary>
    public SlideStyleOverride? StyleOverride { get; }

    public Slide(string content, SlideType type, string label, string? mediaPath = null, int? themeId = null, SlideContext? context = null, SlideStyleOverride? styleOverride = null)
    {
        if (type is not SlideType.Media and not SlideType.Blank && string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required for non-media slides.", nameof(content));

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.", nameof(label));

        Content = content;
        Type = type;
        Label = label;
        MediaPath = mediaPath;
        ThemeId = themeId;
        Context = context ?? SlideContext.Empty;
        StyleOverride = styleOverride;
    }

    /// <summary>
    /// Returns a copy with <see cref="StyleOverride"/> replaced. Used by Stage View's live quick-fix
    /// (F7) to patch already-generated slides in place, without regenerating them from the source song.
    /// </summary>
    public Slide WithStyleOverride(SlideStyleOverride? styleOverride) =>
        new(Content, Type, Label, MediaPath, ThemeId, Context, styleOverride);

    public static Slide Blank() => new(string.Empty, SlideType.Blank, "Blank");
}
