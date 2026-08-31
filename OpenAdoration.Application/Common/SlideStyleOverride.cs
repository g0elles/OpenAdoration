namespace OpenAdoration.Application.Common;

/// <summary>
/// Ad-hoc, non-persisted rendering override for F7 (Stage View quick style fix): lets an operator
/// bump the font size or swap the text/background colour of the LIVE slide deck for the rest of the
/// item, without touching the underlying <see cref="OpenAdoration.Domain.Entities.Theme"/> row.
/// Applied on top of whatever theme the slide resolves to via <see cref="Slide.ThemeId"/> — a null
/// field here falls back to that theme's value. Never written to the database.
/// </summary>
public sealed record SlideStyleOverride(double? FontSize = null, string? FontColor = null, string? BackgroundColor = null);
