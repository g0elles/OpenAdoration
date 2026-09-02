using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public sealed class NoteService : INoteService
{
    private readonly INoteRepository _repository;
    private readonly ILogger<NoteService> _logger;

    public NoteService(INoteRepository repository, ILogger<NoteService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Note?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var note = await _repository.GetByIdAsync(id, ct);

        if (note is null)
            _logger.LogWarning("Note {NoteId} was not found", id);

        return note;
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default) =>
        await _repository.GetAllAsync(ct);

    public async Task<IReadOnlyList<Note>> SearchByTitleAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);
        return await _repository.SearchByTitleAsync(term, ct);
    }

    public async Task<Note> CreateAsync(Note note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        try
        {
            return await _repository.AddAsync(note, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create note: {Title}", note.Title);
            throw;
        }
    }

    public async Task UpdateAsync(Note note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        try
        {
            await _repository.UpdateAsync(note, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update note {NoteId}", note.Id);
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
            _logger.LogError(ex, "Failed to delete note {NoteId}", id);
            throw;
        }
    }

    public async Task SetThemeIdAsync(int noteId, int? themeId, CancellationToken ct = default)
    {
        try
        {
            await _repository.SetThemeIdAsync(noteId, themeId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set theme for note {NoteId}", noteId);
            throw;
        }
    }

    public IReadOnlyList<Slide> GenerateSlides(Note note, int? themeId = null)
    {
        ArgumentNullException.ThrowIfNull(note);
        return NotesSlideGenerator.GenerateSlides(note.Content, themeId);
    }
}
