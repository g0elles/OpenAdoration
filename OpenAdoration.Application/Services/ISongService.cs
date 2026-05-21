using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface ISongService
{
    Task<Song?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default);
    Task<Song> CreateAsync(Song song, CancellationToken ct = default);
    Task UpdateAsync(Song song, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Generates the ordered list of projection slides for a song.
    /// Pass <paramref name="themeId"/> to override the default theme on every generated slide.
    /// When null, the default theme is used.
    /// </summary>
    IReadOnlyList<Slide> GenerateSlides(Song song, int? themeId = null);
}
