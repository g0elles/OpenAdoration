using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public sealed class WorshipServiceService : IWorshipServiceService
{
    private readonly IWorshipServiceRepository _repository;
    private readonly ILogger<WorshipServiceService> _logger;

    public WorshipServiceService(IWorshipServiceRepository repository, ILogger<WorshipServiceService> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    public async Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all worship services");
        var services = await _repository.GetAllAsync(ct);
        _logger.LogDebug("Returned {Count} worship service(s)", services.Count);
        return services;
    }

    public async Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching worship service {ServiceId}", id);
        var service = await _repository.GetByIdAsync(id, ct);
        if (service is null)
            _logger.LogWarning("Worship service {ServiceId} was not found", id);
        return service;
    }

    public async Task<WorshipService> CreateAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        _logger.LogInformation("Creating worship service: {Name} on {Date:d}", service.Name, service.Date);

        try
        {
            var created = await _repository.AddAsync(service, ct);
            _logger.LogInformation("Worship service created with ID {ServiceId}", created.Id);
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create worship service: {Name}", service.Name);
            throw;
        }
    }

    public async Task UpdateAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        _logger.LogInformation("Updating worship service {ServiceId}", service.Id);

        try
        {
            await _repository.UpdateAsync(service, ct);
            _logger.LogInformation("Worship service {ServiceId} updated", service.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worship service {ServiceId}", service.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting worship service {ServiceId}", id);

        try
        {
            await _repository.DeleteAsync(id, ct);
            _logger.LogInformation("Worship service {ServiceId} deleted", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete worship service {ServiceId}", id);
            throw;
        }
    }
}
