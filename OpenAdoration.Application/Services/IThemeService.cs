using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IThemeService
{
    Task<IReadOnlyList<Theme>> GetAllAsync(CancellationToken ct = default);
    Task<Theme?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Theme> GetDefaultAsync(CancellationToken ct = default);
    Task<Theme> CreateAsync(Theme theme, CancellationToken ct = default);
    Task UpdateAsync(Theme theme, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
