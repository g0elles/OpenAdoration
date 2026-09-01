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

    /// <summary>Patches only the schedule item's ThemeId — used by Stage View's live style editor
    /// when scoped to "this occurrence" rather than the song itself.</summary>
    Task SetItemThemeIdAsync(int itemId, int? themeId, CancellationToken ct = default);
    Task<int?> GetItemThemeIdAsync(int itemId, CancellationToken ct = default);

    /// <summary>Reads a song schedule item's verse-order override without loading the full service
    /// graph — used to preserve it when Stage View's live style editor regenerates live slides.</summary>
    Task<string?> GetItemVerseOrderOverrideAsync(int itemId, CancellationToken ct = default);

    /// <summary>Re-point an existing Bible schedule item at a new passage/version in place, keeping its position.</summary>
    Task UpdateBibleItemAsync(int itemId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId, CancellationToken ct = default);
}
