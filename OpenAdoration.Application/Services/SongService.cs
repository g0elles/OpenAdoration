using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public sealed class SongService : ISongService
{
    private readonly ISongRepository _repository;
    private readonly ILogger<SongService> _logger;

    public SongService(ISongRepository repository, ILogger<SongService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Song?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var song = await _repository.GetByIdAsync(id, ct);

        if (song is null)
            _logger.LogWarning("Song {SongId} was not found", id);

        return song;
    }

    public async Task<Song?> GetBySourceGuidAsync(string sourceGuid, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGuid);
        return await _repository.GetBySourceGuidAsync(sourceGuid, ct);
    }

    public async Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default)
    {
        var songs = await _repository.GetAllAsync(ct);

        return songs;
    }

    public async Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        var results = await _repository.SearchByTitleAsync(term, ct);

        return results;
    }

    public async Task<IReadOnlyList<Song>> SearchByLyricsAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        try
        {
            return await _repository.SearchByLyricsAsync(term, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lyrics FTS search failed for term '{Term}' — index may not exist yet", term);
            return [];
        }
    }

    public async Task<Song> CreateAsync(Song song, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        try
        {
            var created = await _repository.AddAsync(song, ct);
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create song: {Title}", song.Title);
            throw;
        }
    }

    public async Task UpdateAsync(Song song, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);


        try
        {
            await _repository.UpdateAsync(song, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update song {SongId}", song.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {

        try
        {
            await _repository.DeleteAsync(id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete song {SongId}", id);
            throw;
        }
    }

    public IReadOnlyList<Slide> GenerateSlides(Song song, int? themeId = null, string? verseOrderOverride = null)
    {
        ArgumentNullException.ThrowIfNull(song);

        var ordered = song.GetOrderedSections(verseOrderOverride);

        if (ordered.Count == 0)
        {
            _logger.LogWarning("Song {SongId} '{Title}' has no sections — no slides generated", song.Id, song.Title);
            return [];
        }

        // Skip sections that have no lyrics — they cannot form a valid slide and
        // would throw ArgumentException in the Slide constructor.
        var slides = ordered
            .Where(s => !string.IsNullOrWhiteSpace(s.Lyrics))
            .Select(s => new Slide(
                content: s.Lyrics,
                type: SlideType.Song,
                label: s.Label,
                themeId: themeId,
                context: new SlideContext
                {
                    SongTitle      = song.Title,
                    SongAuthor     = song.Author,
                    SongVerseTag   = s.Label,
                    SongCopyright  = song.Copyright,
                    SongCcliNumber = song.CcliNumber
                }))
            .ToList();

        var skipped = ordered.Count - slides.Count;
        if (skipped > 0)
            _logger.LogWarning("Song {SongId} '{Title}': skipped {Skipped} section(s) with empty lyrics", song.Id, song.Title, skipped);

        return slides;
    }
}
