using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IWorshipServiceService
{
    Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default);
    Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<WorshipService> CreateAsync(WorshipService service, CancellationToken ct = default);
    Task UpdateAsync(WorshipService service, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
