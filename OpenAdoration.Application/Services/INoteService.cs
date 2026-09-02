using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface INoteService
{
    Task<Note?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Note>> SearchByTitleAsync(string term, CancellationToken ct = default);
    Task<Note> CreateAsync(Note note, CancellationToken ct = default);
    Task UpdateAsync(Note note, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Patches only <see cref="Note.ThemeId"/> — used by Stage View's live style editor.</summary>
    Task SetThemeIdAsync(int noteId, int? themeId, CancellationToken ct = default);

    /// <summary>
    /// Generates one slide per blank-line-separated paragraph in <see cref="Note.Content"/>.
    /// Pass <paramref name="themeId"/> to override the default theme on every generated slide.
    /// </summary>
    IReadOnlyList<Slide> GenerateSlides(Note note, int? themeId = null);
}
