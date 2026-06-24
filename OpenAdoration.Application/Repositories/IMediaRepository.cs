using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface IMediaRepository
{
    /// <summary>General (non-background) media, projectable as slides.</summary>
    Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Background media (theme backgrounds), the exclusive other category.</summary>
    Task<IReadOnlyList<MediaFile>> GetBackgroundsAsync(CancellationToken ct = default);
    Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MediaFile?> GetByContentHashAsync(string contentHash, bool isBackground = false, CancellationToken ct = default);
    Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
