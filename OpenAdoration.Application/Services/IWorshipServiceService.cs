using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IWorshipServiceService
{
    Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default);
    Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<WorshipService?> GetWithItemsAsync(int serviceId, CancellationToken ct = default);
    Task<WorshipService> CreateAsync(WorshipService service, CancellationToken ct = default);
    Task UpdateAsync(WorshipService service, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, CancellationToken ct = default);
    Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, CancellationToken ct = default);
    Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, CancellationToken ct = default);
    Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default);
    Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default);
}
