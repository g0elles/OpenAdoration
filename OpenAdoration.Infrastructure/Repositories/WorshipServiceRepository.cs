using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class WorshipServiceRepository : IWorshipServiceRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public WorshipServiceRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.WorshipServices
            .AsNoTracking()
            .Include(ws => ws.Items.OrderBy(i => i.Order))
            .FirstOrDefaultAsync(ws => ws.Id == id, ct);
    }

    public async Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.WorshipServices
            .AsNoTracking()
            .OrderByDescending(ws => ws.Date)
            .ToListAsync(ct);
    }

    public async Task<WorshipService> AddAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(service.Name))
            throw new ArgumentException("Service name is required.", nameof(service));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.WorshipServices.Add(service);
        await context.SaveChangesAsync(ct);

        return service;
    }

    public async Task UpdateAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(service.Name))
            throw new ArgumentException("Service name is required.", nameof(service));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.WorshipServices
            .Include(ws => ws.Items)
            .FirstOrDefaultAsync(ws => ws.Id == service.Id, ct)
            ?? throw new InvalidOperationException($"WorshipService with ID {service.Id} was not found.");

        existing.Name = service.Name;
        existing.Date = service.Date;

        // Replace all schedule items to avoid tracking conflicts across subtypes
        context.ScheduleItems.RemoveRange(existing.Items);
        foreach (var item in service.Items)
        {
            item.Id = 0;
            item.ServiceId = existing.Id;
            existing.Items.Add(item);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var service = await context.WorshipServices.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"WorshipService with ID {id} was not found.");

        context.WorshipServices.Remove(service);
        await context.SaveChangesAsync(ct);
    }

    public async Task<WorshipService?> GetWithItemsAsync(int serviceId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var service = await context.WorshipServices
            .Include(ws => ws.Items.OrderBy(i => i.Order))
            .FirstOrDefaultAsync(ws => ws.Id == serviceId, ct);

        if (service is null) return null;

        // Populate nav properties for each subtype within this context
        var songIds = service.Items.OfType<SongScheduleItem>().Select(i => i.SongId).ToList();
        if (songIds.Count > 0)
        {
            var songs = await context.Songs
                .Include(s => s.Sections)
                .Where(s => songIds.Contains(s.Id))
                .ToListAsync(ct);
            var songMap = songs.ToDictionary(s => s.Id);
            foreach (var item in service.Items.OfType<SongScheduleItem>())
            {
                if (!songMap.TryGetValue(item.SongId, out var song))
                    throw new InvalidOperationException(
                        $"SongScheduleItem {item.Id} references missing Song {item.SongId}.");
                item.Song = song;
            }
        }

        var versionIds = service.Items.OfType<BibleScheduleItem>()
            .Where(i => i.BibleVersionId.HasValue)
            .Select(i => i.BibleVersionId!.Value)
            .Distinct().ToList();
        if (versionIds.Count > 0)
        {
            var versions = await context.BibleVersions
                .Where(v => versionIds.Contains(v.Id))
                .ToListAsync(ct);
            var versionMap = versions.ToDictionary(v => v.Id);
            foreach (var item in service.Items.OfType<BibleScheduleItem>().Where(i => i.BibleVersionId.HasValue))
            {
                if (!versionMap.TryGetValue(item.BibleVersionId!.Value, out var version))
                    throw new InvalidOperationException(
                        $"BibleScheduleItem {item.Id} references missing BibleVersion {item.BibleVersionId.Value}.");
                item.BibleVersion = version;
            }
        }

        var mediaIds = service.Items.OfType<MediaScheduleItem>().Select(i => i.MediaFileId).ToList();
        if (mediaIds.Count > 0)
        {
            var mediaFiles = await context.MediaFiles
                .Where(m => mediaIds.Contains(m.Id))
                .ToListAsync(ct);
            var mediaMap = mediaFiles.ToDictionary(m => m.Id);
            foreach (var item in service.Items.OfType<MediaScheduleItem>())
            {
                if (!mediaMap.TryGetValue(item.MediaFileId, out var mediaFile))
                    throw new InvalidOperationException(
                        $"MediaScheduleItem {item.Id} references missing MediaFile {item.MediaFileId}.");
                item.MediaFile = mediaFile;
            }
        }

        return service;
    }

    public async Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var nextOrder = await context.ScheduleItems
            .Where(i => i.ServiceId == serviceId)
            .Select(i => (int?)i.Order)
            .MaxAsync(ct) ?? -1;

        context.ScheduleItems.Add(new SongScheduleItem
        {
            ServiceId = serviceId,
            SongId    = songId,
            ThemeId   = themeId,
            Order     = nextOrder + 1
        });
        await context.SaveChangesAsync(ct);
    }

    public async Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var nextOrder = await context.ScheduleItems
            .Where(i => i.ServiceId == serviceId)
            .Select(i => (int?)i.Order)
            .MaxAsync(ct) ?? -1;

        context.ScheduleItems.Add(new BibleScheduleItem
        {
            ServiceId     = serviceId,
            Book          = book,
            Chapter       = chapter,
            VerseStart    = verseStart,
            VerseEnd      = verseEnd,
            BibleVersionId = bibleVersionId,
            ThemeId       = themeId,
            Order         = nextOrder + 1
        });
        await context.SaveChangesAsync(ct);
    }

    public async Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var nextOrder = await context.ScheduleItems
            .Where(i => i.ServiceId == serviceId)
            .Select(i => (int?)i.Order)
            .MaxAsync(ct) ?? -1;

        context.ScheduleItems.Add(new MediaScheduleItem
        {
            ServiceId   = serviceId,
            MediaFileId = mediaFileId,
            ThemeId     = themeId,
            Order       = nextOrder + 1
        });
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var item = await context.ScheduleItems.FindAsync([scheduleItemId], ct)
            ?? throw new InvalidOperationException($"ScheduleItem with ID {scheduleItemId} was not found.");

        context.ScheduleItems.Remove(item);
        await context.SaveChangesAsync(ct);
    }

    public async Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var items = await context.ScheduleItems
            .Where(i => i.ServiceId == serviceId)
            .ToListAsync(ct);

        var actualIds   = items.Select(i => i.Id).ToHashSet();
        var providedIds = new HashSet<int>(orderedItemIds);
        if (!actualIds.SetEquals(providedIds))
            throw new InvalidOperationException(
                $"ReorderItemsAsync: provided IDs do not exactly match service {serviceId}'s items " +
                $"(expected {actualIds.Count}, got {orderedItemIds.Count} with {providedIds.Except(actualIds).Count()} unknown).");

        var indexMap = items.ToDictionary(i => i.Id);
        for (var index = 0; index < orderedItemIds.Count; index++)
            indexMap[orderedItemIds[index]].Order = index;

        await context.SaveChangesAsync(ct);
    }

    public async Task SetItemAutoAdvanceAsync(int itemId, int? autoAdvanceSeconds, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var item = await context.ScheduleItems.FindAsync([itemId], ct)
            ?? throw new InvalidOperationException($"ScheduleItem with ID {itemId} was not found.");

        item.AutoAdvanceSeconds = autoAdvanceSeconds > 0 ? autoAdvanceSeconds : null;
        await context.SaveChangesAsync(ct);
    }
}
