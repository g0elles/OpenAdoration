using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface ISongRepository
{
    Task<Song?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default);
    Task<IReadOnlyList<Song>> SearchByLyricsAsync(string term, CancellationToken ct = default);
    Task<Song> AddAsync(Song song, CancellationToken ct = default);
    Task UpdateAsync(Song song, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
