using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Schedule;

/// <summary>
/// F7 rebuild: <c>ThemeCascade.ForSong</c> reads a schedule item's own ThemeId, but until now
/// nothing ever wrote it after item creation — <see cref="WorshipServiceRepository.SetItemThemeIdAsync"/>
/// and <see cref="WorshipServiceRepository.GetItemThemeIdAsync"/> are that missing narrow read/write
/// path, mirroring the existing <c>SetItemAutoAdvanceAsync</c> pattern (patch one column, no
/// destructive whole-service replace).
/// </summary>
public sealed class WorshipServiceRepositoryThemeIdTests : IDisposable
{
    private readonly SqliteFactory _factory = new();

    private async Task<(WorshipServiceRepository Repo, int ServiceId, int ItemId)> SeedAsync()
    {
        var repo = new WorshipServiceRepository(_factory);
        var songRepo = new SongRepository(_factory);

        var song = await songRepo.AddAsync(new Song { Title = "Song", Sections = [] });
        var service = await repo.AddAsync(new WorshipService { Name = "Sunday Service", Date = DateTime.UtcNow });
        await repo.AddSongItemAsync(service.Id, song.Id);

        var withItems = await repo.GetWithItemsAsync(service.Id);
        var itemId = Assert.Single(withItems!.Items).Id;

        return (repo, service.Id, itemId);
    }

    private async Task<int> SeedThemeAsync() =>
        (await new ThemeRepository(_factory).AddAsync(new Theme { Name = "Custom" })).Id;

    [Fact]
    public async Task SetItemThemeIdAsync_PatchesOnlyThemeId_LeavesOrderAndAutoAdvanceUntouched()
    {
        var (repo, serviceId, itemId) = await SeedAsync();
        var themeId = await SeedThemeAsync();
        await repo.SetItemAutoAdvanceAsync(itemId, autoAdvanceSeconds: 15);

        await repo.SetItemThemeIdAsync(itemId, themeId);

        Assert.Equal(themeId, await repo.GetItemThemeIdAsync(itemId));
        var reloadedItem = Assert.Single((await repo.GetWithItemsAsync(serviceId))!.Items);
        Assert.Equal(15, reloadedItem.AutoAdvanceSeconds);
        Assert.Equal(0, reloadedItem.Order);
    }

    [Fact]
    public async Task SetItemThemeIdAsync_Null_ClearsTheme()
    {
        var (repo, _, itemId) = await SeedAsync();
        var themeId = await SeedThemeAsync();
        await repo.SetItemThemeIdAsync(itemId, themeId);

        await repo.SetItemThemeIdAsync(itemId, themeId: null);

        Assert.Null(await repo.GetItemThemeIdAsync(itemId));
    }

    [Fact]
    public async Task GetItemThemeIdAsync_UnsetItem_ReturnsNull()
    {
        var (repo, _, itemId) = await SeedAsync();
        Assert.Null(await repo.GetItemThemeIdAsync(itemId));
    }

    [Fact]
    public async Task SetItemThemeIdAsync_UnknownItem_Throws()
    {
        var repo = new WorshipServiceRepository(_factory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SetItemThemeIdAsync(999, themeId: 1));
    }

    [Fact]
    public async Task GetItemVerseOrderOverrideAsync_ReadsWhatSetItemVerseOrderOverrideWrote()
    {
        var (repo, _, itemId) = await SeedAsync();

        await repo.SetItemVerseOrderOverrideAsync(itemId, "2,1,3");

        Assert.Equal("2,1,3", await repo.GetItemVerseOrderOverrideAsync(itemId));
    }

    [Fact]
    public async Task GetItemVerseOrderOverrideAsync_UnsetItem_ReturnsNull()
    {
        var (repo, _, itemId) = await SeedAsync();
        Assert.Null(await repo.GetItemVerseOrderOverrideAsync(itemId));
    }

    public void Dispose() => _factory.Dispose();

    private sealed class SqliteFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;

        public SqliteFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var ctx = CreateDbContext();
            ctx.Database.EnsureCreated();
        }

        public AppDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

        public void Dispose() => _connection.Dispose();
    }
}
