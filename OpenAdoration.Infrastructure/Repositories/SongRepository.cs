using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class SongRepository : ISongRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SongRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Song?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Songs
            .AsNoTracking()
            .Include(s => s.Sections.OrderBy(ss => ss.Order))
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Song?> GetBySourceGuidAsync(string sourceGuid, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGuid);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Songs
            .AsNoTracking()
            .Include(s => s.Sections.OrderBy(ss => ss.Order))
            .FirstOrDefaultAsync(s => s.SourceGuid == sourceGuid, ct);
    }

    public async Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Songs
            .AsNoTracking()
            .Include(s => s.Sections.OrderBy(ss => ss.Order))
            .OrderBy(s => s.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // EF.Functions.Like translates to SQLite LIKE, which is case-insensitive for
        // ASCII by default -- no need for ToLower() on either side.
        // Matches against both Title and Author so operators can search "Tomlin" or
        // "Amazing Grace" interchangeably (P3 roadmap checklist fix).
        var pattern = $"%{term}%";
        return await context.Songs
            .AsNoTracking()
            .Include(s => s.Sections.OrderBy(ss => ss.Order))
            .Where(s => EF.Functions.Like(s.Title, pattern)
                     || (s.Author != null && EF.Functions.Like(s.Author, pattern)))
            .OrderBy(s => s.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Song>> SearchByLyricsAsync(string term, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var songIds = await FtsLyricsSearchAsync(context, term, ct);
        if (songIds.Count == 0) return [];

        return await context.Songs
            .AsNoTracking()
            .Include(s => s.Sections.OrderBy(ss => ss.Order))
            .Where(s => songIds.Contains(s.Id))
            .OrderBy(s => s.Title)
            .ToListAsync(ct);
    }

    private static async Task<List<int>> FtsLyricsSearchAsync(
        AppDbContext context, string term, CancellationToken ct)
    {
        var conn = (SqliteConnection)context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT ss.SongId
            FROM   SongSectionsFts fts
            JOIN   SongSections ss ON ss.Id = fts.rowid
            WHERE  SongSectionsFts MATCH @term
            ORDER  BY ss.SongId
            """;
        cmd.Parameters.AddWithValue("@term", EscapeFtsTerm(term));

        var ids = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetInt32(0));

        return ids;
    }

    // Each word gets a trailing * for prefix matching so "cura" matches "curará", "curas", etc.
    // Wrap the word in double quotes (FTS5 string literal) so punctuation/operators inside it
    // are treated as content, not query syntax — `"grace,"*` won't throw. Embedded quotes are
    // doubled per FTS5 escaping. Mirrors BibleRepository.BuildFtsTerm.
    private static string EscapeFtsTerm(string raw)
    {
        var words = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w => $"\"{w.Replace("\"", "\"\"")}\"*"));
    }

    public async Task<Song> AddAsync(Song song, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        if (string.IsNullOrWhiteSpace(song.Title))
            throw new ArgumentException("Song title is required.", nameof(song));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.Songs.Add(song);
        await context.SaveChangesAsync(ct);

        return song;
    }

    public async Task UpdateAsync(Song song, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        if (string.IsNullOrWhiteSpace(song.Title))
            throw new ArgumentException("Song title is required.", nameof(song));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Songs
            .Include(s => s.Sections)
            .FirstOrDefaultAsync(s => s.Id == song.Id, ct)
            ?? throw new InvalidOperationException($"Song with ID {song.Id} was not found.");

        existing.Title          = song.Title;
        existing.Author         = song.Author;
        existing.Classification = song.Classification;
        existing.VerseOrder     = song.VerseOrder;
        existing.Copyright      = song.Copyright;
        existing.CcliNumber     = song.CcliNumber;

        // Replace sections entirely to avoid stale or orphaned section rows
        context.SongSections.RemoveRange(existing.Sections);
        foreach (var section in song.Sections)
        {
            section.Id = 0; // Force insert, not update
            section.SongId = existing.Id;
            existing.Sections.Add(section);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var song = await context.Songs.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Song with ID {id} was not found.");

        context.Songs.Remove(song);
        await context.SaveChangesAsync(ct);
    }
}
