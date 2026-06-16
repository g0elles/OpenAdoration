using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface IMediaRepository
{
    Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default);
    Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MediaFile?> GetByContentHashAsync(string contentHash, CancellationToken ct = default);
    Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
