using Microsoft.EntityFrameworkCore;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;

namespace OpenAdoration.Infrastructure.Repositories;

public sealed class ThemeRepository : IThemeRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ThemeRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Theme>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Themes
            .AsNoTracking()
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<Theme?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Themes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Theme?> GetDefaultAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Themes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsDefault, ct);
    }

    public async Task<Theme> AddAsync(Theme theme, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (string.IsNullOrWhiteSpace(theme.Name))
            throw new ArgumentException("Theme name is required.", nameof(theme));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        if (theme.IsDefault)
            await ClearDefaultFlagAsync(context, excludeId: null, ct);

        context.Themes.Add(theme);
        await context.SaveChangesAsync(ct);

        return theme;
    }

    public async Task UpdateAsync(Theme theme, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (string.IsNullOrWhiteSpace(theme.Name))
            throw new ArgumentException("Theme name is required.", nameof(theme));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Themes.FindAsync([theme.Id], ct)
            ?? throw new InvalidOperationException($"Theme with ID {theme.Id} was not found.");

        if (theme.IsDefault)
            await ClearDefaultFlagAsync(context, excludeId: theme.Id, ct);

        existing.Name                = theme.Name;
        existing.FontFamily          = theme.FontFamily;
        existing.FontSize            = theme.FontSize;
        existing.FontColor           = theme.FontColor;
        existing.TextAlignment       = theme.TextAlignment;
        existing.BackgroundColor     = theme.BackgroundColor;
        existing.BackgroundImagePath = theme.BackgroundImagePath;
        existing.BackgroundVideoPath = theme.BackgroundVideoPath;
        existing.IsDefault           = theme.IsDefault;
        existing.HeaderTemplate      = theme.HeaderTemplate;
        existing.FooterTemplate      = theme.FooterTemplate;
        existing.SlideTransition     = theme.SlideTransition;

        await context.SaveChangesAsync(ct);
    }

    public async Task SetDefaultAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var theme = await context.Themes.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Theme with ID {id} was not found.");

        await ClearDefaultFlagAsync(context, excludeId: id, ct);
        theme.IsDefault = true;
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var theme = await context.Themes.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Theme with ID {id} was not found.");

        if (theme.IsDefault)
            throw new InvalidOperationException(
                "The default theme cannot be deleted. Assign another theme as default first.");

        context.Themes.Remove(theme);
        await context.SaveChangesAsync(ct);
    }

    // Ensures only one theme carries IsDefault = true at any time
    private static async Task ClearDefaultFlagAsync(
        AppDbContext context, int? excludeId, CancellationToken ct)
    {
        var currentDefaults = await context.Themes
            .Where(t => t.IsDefault && (excludeId == null || t.Id != excludeId))
            .ToListAsync(ct);

        foreach (var t in currentDefaults)
            t.IsDefault = false;
    }
}
