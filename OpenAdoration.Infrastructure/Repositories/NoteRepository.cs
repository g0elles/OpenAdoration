using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class NoteRepository : INoteRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public NoteRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Note?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Notes
            .AsNoTracking()
            .OrderBy(n => n.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Note>> SearchByTitleAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var pattern = $"%{term}%";
        return await context.Notes
            .AsNoTracking()
            .Where(n => EF.Functions.Like(n.Title, pattern))
            .OrderBy(n => n.Title)
            .ToListAsync(ct);
    }

    public async Task<Note> AddAsync(Note note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (string.IsNullOrWhiteSpace(note.Title))
            throw new ArgumentException("Note title is required.", nameof(note));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.Notes.Add(note);
        await context.SaveChangesAsync(ct);

        return note;
    }

    public async Task UpdateAsync(Note note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (string.IsNullOrWhiteSpace(note.Title))
            throw new ArgumentException("Note title is required.", nameof(note));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Notes.FirstOrDefaultAsync(n => n.Id == note.Id, ct)
            ?? throw new InvalidOperationException($"Note with ID {note.Id} was not found.");

        existing.Title   = note.Title;
        existing.Content = note.Content;
        existing.ThemeId = note.ThemeId;

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var note = await context.Notes.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Note with ID {id} was not found.");

        context.Notes.Remove(note);
        await context.SaveChangesAsync(ct);
    }

    public async Task SetThemeIdAsync(int noteId, int? themeId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var note = await context.Notes.FindAsync([noteId], ct)
            ?? throw new InvalidOperationException($"Note with ID {noteId} was not found.");

        note.ThemeId = themeId;
        await context.SaveChangesAsync(ct);
    }
}
