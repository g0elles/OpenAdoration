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

    public Slide(string content, SlideType type, string label, string? mediaPath = null, int? themeId = null)
    {
        if (type != SlideType.Media && string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required for non-media slides.", nameof(content));

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.", nameof(label));

        Content = content;
        Type = type;
        Label = label;
        MediaPath = mediaPath;
        ThemeId = themeId;
    }

    public static Slide Blank() => new(string.Empty, SlideType.Blank, "Blank");
}
