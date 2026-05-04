using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public sealed class ThemeService : IThemeService
{
    private readonly IThemeRepository _repository;
    private readonly ILogger<ThemeService> _logger;

    public ThemeService(IThemeRepository repository, ILogger<ThemeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Theme>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all themes");
        return await _repository.GetAllAsync(ct);
    }

    public async Task<Theme?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching theme {ThemeId}", id);
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<Theme> GetDefaultAsync(CancellationToken ct = default)
    {
        var theme = await _repository.GetDefaultAsync(ct);

        if (theme is null)
        {
            // Should never happen — seed data guarantees a default theme exists
            _logger.LogCritical("No default theme found in the database. The seed data may be missing.");
            throw new InvalidOperationException(
                "No default theme is configured. The database may be corrupted or the migration is incomplete.");
        }

        return theme;
    }

    public async Task<Theme> CreateAsync(Theme theme, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _logger.LogInformation("Creating theme: {Name}", theme.Name);

        try
        {
            var created = await _repository.AddAsync(theme, ct);
            _logger.LogInformation("Theme created with ID {ThemeId}: {Name}", created.Id, created.Name);
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create theme: {Name}", theme.Name);
            throw;
        }
    }

    public async Task UpdateAsync(Theme theme, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _logger.LogInformation("Updating theme {ThemeId}: {Name}", theme.Id, theme.Name);

        try
        {
            await _repository.UpdateAsync(theme, ct);
            _logger.LogInformation("Theme {ThemeId} updated successfully", theme.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update theme {ThemeId}", theme.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting theme {ThemeId}", id);

        try
        {
            await _repository.DeleteAsync(id, ct);
            _logger.LogInformation("Theme {ThemeId} deleted", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete theme {ThemeId}", id);
            throw;
        }
    }
}
