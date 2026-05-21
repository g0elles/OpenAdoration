using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Repositories;

public interface IThemeRepository
{
    Task<IReadOnlyList<Theme>> GetAllAsync(CancellationToken ct = default);
    Task<Theme?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Theme?> GetDefaultAsync(CancellationToken ct = default);
    Task<Theme> AddAsync(Theme theme, CancellationToken ct = default);
    Task UpdateAsync(Theme theme, CancellationToken ct = default);
    Task SetDefaultAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
