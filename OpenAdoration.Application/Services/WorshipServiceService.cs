using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public sealed class WorshipServiceService : IWorshipServiceService
{
    private readonly IWorshipServiceRepository _repository;
    private readonly ILogger<WorshipServiceService> _logger;

    public WorshipServiceService(IWorshipServiceRepository repository, ILogger<WorshipServiceService> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    public async Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all worship services");
        var services = await _repository.GetAllAsync(ct);
        _logger.LogDebug("Returned {Count} worship service(s)", services.Count);
        return services;
    }

    public async Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching worship service {ServiceId}", id);
        var service = await _repository.GetByIdAsync(id, ct);
        if (service is null)
            _logger.LogWarning("Worship service {ServiceId} was not found", id);
        return service;
    }

    public async Task<WorshipService?> GetBySourceGuidAsync(string sourceGuid, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGuid);
        return await _repository.GetBySourceGuidAsync(sourceGuid, ct);
    }

    public async Task<WorshipService> CreateAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        _logger.LogInformation("Creating worship service: {Name} on {Date:d}", service.Name, service.Date);

        try
        {
            var created = await _repository.AddAsync(service, ct);
            _logger.LogInformation("Worship service created with ID {ServiceId}", created.Id);
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create worship service: {Name}", service.Name);
            throw;
        }
    }

    public async Task UpdateAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        _logger.LogInformation("Updating worship service {ServiceId}", service.Id);

        try
        {
            await _repository.UpdateAsync(service, ct);
            _logger.LogInformation("Worship service {ServiceId} updated", service.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worship service {ServiceId}", service.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting worship service {ServiceId}", id);

        try
        {
            await _repository.DeleteAsync(id, ct);
            _logger.LogInformation("Worship service {ServiceId} deleted", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete worship service {ServiceId}", id);
            throw;
        }
    }

    public async Task<WorshipService?> GetWithItemsAsync(int serviceId, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching worship service {ServiceId} with items", serviceId);
        return await _repository.GetWithItemsAsync(serviceId, ct);
    }

    public async Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding song {SongId} to service {ServiceId}", songId, serviceId);
        try
        {
            await _repository.AddSongItemAsync(serviceId, songId, themeId, autoAdvanceSeconds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add song {SongId} to service {ServiceId}", songId, serviceId);
            throw;
        }
    }

    public async Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding Bible {Book} {Chapter}:{VerseStart}-{VerseEnd} to service {ServiceId}", book, chapter, verseStart, verseEnd, serviceId);
        try
        {
            await _repository.AddBibleItemAsync(serviceId, book, chapter, verseStart, verseEnd, bibleVersionId, themeId, autoAdvanceSeconds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add Bible item to service {ServiceId}", serviceId);
            throw;
        }
    }

    public async Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding media {MediaFileId} to service {ServiceId}", mediaFileId, serviceId);
        try
        {
            await _repository.AddMediaItemAsync(serviceId, mediaFileId, themeId, autoAdvanceSeconds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add media {MediaFileId} to service {ServiceId}", mediaFileId, serviceId);
            throw;
        }
    }

    public async Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Removing schedule item {ItemId}", scheduleItemId);
        try
        {
            await _repository.RemoveItemAsync(scheduleItemId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove schedule item {ItemId}", scheduleItemId);
            throw;
        }
    }

    public async Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default)
    {
        _logger.LogDebug("Reordering {Count} items in service {ServiceId}", orderedItemIds.Count, serviceId);
        try
        {
            await _repository.ReorderItemsAsync(serviceId, orderedItemIds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder items in service {ServiceId}", serviceId);
            throw;
        }
    }

    public async Task SetItemAutoAdvanceAsync(int itemId, int? autoAdvanceSeconds, CancellationToken ct = default)
    {
        _logger.LogDebug("Setting auto-advance for item {ItemId} to {Seconds}s", itemId, autoAdvanceSeconds);
        try
        {
            await _repository.SetItemAutoAdvanceAsync(itemId, autoAdvanceSeconds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set auto-advance for item {ItemId}", itemId);
            throw;
        }
    }

    public async Task SetItemVerseOrderOverrideAsync(int itemId, string? verseOrderOverride, CancellationToken ct = default)
    {
        _logger.LogDebug("Setting verse order override for item {ItemId}", itemId);
        try
        {
            await _repository.SetItemVerseOrderOverrideAsync(itemId, verseOrderOverride, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set verse order override for item {ItemId}", itemId);
            throw;
        }
    }

    public async Task UpdateBibleItemAsync(
        int itemId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId, CancellationToken ct = default)
    {
        _logger.LogDebug("Updating Bible item {ItemId} to {Book} {Chapter}:{VerseStart}-{VerseEnd}", itemId, book, chapter, verseStart, verseEnd);
        try
        {
            await _repository.UpdateBibleItemAsync(itemId, book, chapter, verseStart, verseEnd, bibleVersionId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Bible item {ItemId}", itemId);
            throw;
        }
    }
}
