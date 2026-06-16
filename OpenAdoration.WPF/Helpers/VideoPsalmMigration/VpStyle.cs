namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>
/// The subset of a VideoPsalm style cascade that OpenAdoration's <c>Theme</c> can represent:
/// body font + color, header/footer token templates, and a single background (image OR video,
/// by basename). Stroke, per-zone fonts, rects, luminosity and font size are intentionally
/// dropped — OA's fixed 3-zone Viewbox model has no equivalent. See VIDEOPSALM_REFERENCE.md §8b.
/// </summary>
public sealed record VpStyle(
    string? FontFamily,
    string? FontColor,
    string? HeaderTemplate,
    string? FooterTemplate,
    string? BackgroundImage,
    string? BackgroundVideo)
{
    public static readonly VpStyle Empty = new(null, null, null, null, null, null);

    public bool HasBackground => BackgroundImage is not null || BackgroundVideo is not null;

    /// <summary>
    /// Overlay a more-specific style on this base: each scalar field wins when the override sets
    /// it; Background is replaced as a unit (a song's video bg fully supersedes the root's image,
    /// it doesn't layer under it).
    /// </summary>
    public VpStyle Merge(VpStyle o) => new(
        o.FontFamily ?? FontFamily,
        o.FontColor ?? FontColor,
        o.HeaderTemplate ?? HeaderTemplate,
        o.FooterTemplate ?? FooterTemplate,
        o.HasBackground ? o.BackgroundImage : BackgroundImage,
        o.HasBackground ? o.BackgroundVideo : BackgroundVideo);
}
