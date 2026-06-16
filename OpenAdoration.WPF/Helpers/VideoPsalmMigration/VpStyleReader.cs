using static OpenAdoration.WPF.Helpers.VideoPsalmMigration.VpRead;

namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>
/// Reads a VideoPsalm style JSON object (RootStyle / &lt;Type&gt;Style / a per-item <c>Style</c>)
/// into the <see cref="VpStyle"/> subset OA can apply. See VIDEOPSALM_REFERENCE.md §8b.
/// </summary>
internal static class VpStyleReader
{
    public static VpStyle Read(IReadOnlyDictionary<string, object?>? style)
    {
        if (style is null) return VpStyle.Empty;

        var body = AsDict(Get(style, "Body"));
        var header = AsDict(Get(style, "Header"));
        var footer = AsDict(Get(style, "Footer"));
        var background = AsDict(Get(style, "Background"));

        return new VpStyle(
            FontFamily:      body is null ? null : NullIfBlank(GetString(body, "FontName")),
            FontColor:       ReadBodyColor(body),
            HeaderTemplate:  header is null ? null : NullIfBlank(GetString(header, "Template")),
            FooterTemplate:  footer is null ? null : NullIfBlank(GetString(footer, "Template")),
            BackgroundImage: background is null ? null : NullIfBlank(GetString(background, "Image")),
            BackgroundVideo: background is null ? null : NullIfBlank(GetString(background, "Video")));
    }

    // Body.FontStyle.Fill.Color is ARGB hex ("FFFFFFFF"); OA wants "#RRGGBB" (alpha dropped).
    private static string? ReadBodyColor(IReadOnlyDictionary<string, object?>? body)
    {
        var fill = AsDict(Get(AsDict(Get(body, "FontStyle")), "Fill"));
        var argb = fill is null ? null : GetString(fill, "Color");
        return argb is { Length: 8 } ? $"#{argb[2..]}" : null;
    }

    private static object? Get(IReadOnlyDictionary<string, object?>? obj, string key) =>
        obj is not null && obj.TryGetValue(key, out var v) ? v : null;
}
