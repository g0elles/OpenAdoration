using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface ISongService
{
    Task<Song?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Song?> GetBySourceGuidAsync(string sourceGuid, CancellationToken ct = default);
    Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default);
    /// <summary>
    /// Full-text lyrics search via FTS5 index. Falls back to empty list if the
    /// SongSectionsFts table does not exist yet (pre-migration databases).
    /// </summary>
    Task<IReadOnlyList<Song>> SearchByLyricsAsync(string term, CancellationToken ct = default);
    Task<Song> CreateAsync(Song song, CancellationToken ct = default);

    /// <summary>
    /// Creates <paramref name="song"/>, or returns the existing song when its
    /// <see cref="Song.SourceGuid"/> already matches one in the library (VideoPsalm dedup).
    /// </summary>
    Task<(Song Song, bool WasReused)> CreateOrReuseAsync(Song song, CancellationToken ct = default);
    Task UpdateAsync(Song song, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Generates the ordered list of projection slides for a song.
    /// Pass <paramref name="themeId"/> to override the default theme on every generated slide.
    /// When null, the default theme is used.
    /// Pass <paramref name="verseOrderOverride"/> to reorder sections for this projection only,
    /// overriding the song's own <see cref="Song.VerseOrder"/>.
    /// </summary>
    IReadOnlyList<Slide> GenerateSlides(Song song, int? themeId = null, string? verseOrderOverride = null);
}
