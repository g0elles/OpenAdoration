using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class WorshipServiceRepository : IWorshipServiceRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public WorshipServiceRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<WorshipService?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.WorshipServices
            .AsNoTracking()
            .Include(ws => ws.Items.OrderBy(i => i.Order))
            .FirstOrDefaultAsync(ws => ws.Id == id, ct);
    }

    public async Task<IReadOnlyList<WorshipService>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.WorshipServices
            .AsNoTracking()
            .OrderByDescending(ws => ws.Date)
            .ToListAsync(ct);
    }

    public async Task<WorshipService> AddAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(service.Name))
            throw new ArgumentException("Service name is required.", nameof(service));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.WorshipServices.Add(service);
        await context.SaveChangesAsync(ct);

        return service;
    }

    public async Task UpdateAsync(WorshipService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(service.Name))
            throw new ArgumentException("Service name is required.", nameof(service));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.WorshipServices
            .Include(ws => ws.Items)
            .FirstOrDefaultAsync(ws => ws.Id == service.Id, ct)
            ?? throw new InvalidOperationException($"WorshipService with ID {service.Id} was not found.");

        existing.Name = service.Name;
        existing.Date = service.Date;

        // Replace all schedule items to avoid tracking conflicts across subtypes
        context.ScheduleItems.RemoveRange(existing.Items);
        foreach (var item in service.Items)
        {
            item.Id = 0;
            item.ServiceId = existing.Id;
            existing.Items.Add(item);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var service = await context.WorshipServices.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"WorshipService with ID {id} was not found.");

        context.WorshipServices.Remove(service);
        await context.SaveChangesAsync(ct);
    }
}
