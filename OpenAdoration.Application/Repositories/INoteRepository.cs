using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Note>> SearchByTitleAsync(string term, CancellationToken ct = default);
    Task<Note> AddAsync(Note note, CancellationToken ct = default);
    Task UpdateAsync(Note note, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Patches only <see cref="Note.ThemeId"/> — avoids routing a single-field change
    /// through <see cref="UpdateAsync"/>.</summary>
    Task SetThemeIdAsync(int noteId, int? themeId, CancellationToken ct = default);
}
