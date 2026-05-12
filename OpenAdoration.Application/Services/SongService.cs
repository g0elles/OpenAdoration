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
        _logger.LogDebug("Fetching song {SongId}", id);

        var song = await _repository.GetByIdAsync(id, ct);

        if (song is null)
            _logger.LogWarning("Song {SongId} was not found", id);

        return song;
    }

    public async Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all songs");

        var songs = await _repository.GetAllAsync(ct);

        _logger.LogDebug("Returned {Count} songs", songs.Count);

        return songs;
    }

    public async Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        _logger.LogDebug("Searching songs by title: {Term}", term);

        var results = await _repository.SearchByTitleAsync(term, ct);

        _logger.LogDebug("Search '{Term}' returned {Count} result(s)", term, results.Count);

        return results;
    }

    public async Task<Song> CreateAsync(Song song, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        _logger.LogInformation("Creating song: {Title}", song.Title);

        try
        {
            var created = await _repository.AddAsync(song, ct);
            _logger.LogInformation("Song created with ID {SongId}: {Title}", created.Id, created.Title);
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

        _logger.LogInformation("Updating song {SongId}: {Title}", song.Id, song.Title);

        try
        {
            await _repository.UpdateAsync(song, ct);
            _logger.LogInformation("Song {SongId} updated successfully", song.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update song {SongId}", song.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting song {SongId}", id);

        try
        {
            await _repository.DeleteAsync(id, ct);
            _logger.LogInformation("Song {SongId} deleted successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete song {SongId}", id);
            throw;
        }
    }

    public IReadOnlyList<Slide> GenerateSlides(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);

        var ordered = song.GetOrderedSections();

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
                label: s.Label))
            .ToList();

        var skipped = ordered.Count - slides.Count;
        if (skipped > 0)
            _logger.LogWarning("Song {SongId} '{Title}': skipped {Skipped} section(s) with empty lyrics", song.Id, song.Title, skipped);

        _logger.LogDebug("Generated {Count} slide(s) for song {SongId}", slides.Count, song.Id);

        return slides;
    }
}
