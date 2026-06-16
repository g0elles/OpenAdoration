using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IWorshipServiceService
{
    Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default);
    Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<WorshipService?> GetBySourceGuidAsync(string sourceGuid, CancellationToken ct = default);
    Task<WorshipService?> GetWithItemsAsync(int serviceId, CancellationToken ct = default);
    Task<WorshipService> CreateAsync(WorshipService service, CancellationToken ct = default);
    Task UpdateAsync(WorshipService service, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default);
    Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default);
    Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default);
    Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default);
    Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default);
    Task SetItemAutoAdvanceAsync(int itemId, int? autoAdvanceSeconds, CancellationToken ct = default);
    Task SetItemVerseOrderOverrideAsync(int itemId, string? verseOrderOverride, CancellationToken ct = default);

    /// <summary>Point an existing Bible schedule item at an installed version (or null), keeping its position.</summary>
    Task SetItemBibleVersionAsync(int itemId, int? bibleVersionId, CancellationToken ct = default);
}
