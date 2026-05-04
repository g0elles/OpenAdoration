using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface IWorshipServiceRepository
{
    Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default);
    Task<WorshipService> AddAsync(WorshipService service, CancellationToken ct = default);
    Task UpdateAsync(WorshipService service, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
