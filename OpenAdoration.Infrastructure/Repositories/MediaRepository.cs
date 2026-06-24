using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class MediaRepository : IMediaRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public MediaRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.MediaFiles
            .AsNoTracking()
            .Where(mf => !mf.IsBackground)
            .OrderBy(mf => mf.FileName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaFile>> GetBackgroundsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.MediaFiles
            .AsNoTracking()
            .Where(mf => mf.IsBackground)
            .OrderBy(mf => mf.FileName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetAllPathsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.MediaFiles
            .AsNoTracking()
            .Select(mf => mf.FilePath)
            .ToListAsync(ct);
    }

    public async Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(mf => mf.Id == id, ct);
    }

    public async Task<MediaFile?> GetByContentHashAsync(string contentHash, bool isBackground = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(mf => mf.ContentHash == contentHash && mf.IsBackground == isBackground, ct);
    }

    public async Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrWhiteSpace(file.FileName))
            throw new ArgumentException("Media file name is required.", nameof(file));

        if (string.IsNullOrWhiteSpace(file.FilePath))
            throw new ArgumentException("Media file path is required.", nameof(file));

        if (!File.Exists(file.FilePath))
            throw new FileNotFoundException(
                $"The media file was not found on disk: '{file.FilePath}'", file.FilePath);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.MediaFiles.Add(file);
        await context.SaveChangesAsync(ct);

        return file;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var file = await context.MediaFiles.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"MediaFile with ID {id} was not found.");

        context.MediaFiles.Remove(file);
        await context.SaveChangesAsync(ct);
    }
}
