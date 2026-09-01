using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Songs;

/// <summary>
/// F7 rebuild: <see cref="SongRepository.SetThemeIdAsync"/> exists specifically so a live style
/// edit can patch a song's theme without routing through <see cref="SongRepository.UpdateAsync"/>,
/// which destructively replaces every section (G6) just to change one column.
/// </summary>
public sealed class SongRepositorySetThemeIdTests : IDisposable
{
    private readonly SqliteFactory _factory = new();

    private async Task<int> SeedThemeAsync() =>
        (await new ThemeRepository(_factory).AddAsync(new Theme { Name = "Custom" })).Id;

    [Fact]
    public async Task SetThemeIdAsync_PatchesOnlyThemeId_LeavesTitleAndSectionsUntouched()
    {
        var repo = new SongRepository(_factory);
        var themeId = await SeedThemeAsync();
        var created = await repo.AddAsync(new Song
        {
            Title = "Amazing Grace",
            Author = "John Newton",
            Sections = [new SongSection { Type = SectionType.Verse, SectionNumber = 1, Lyrics = "Line one", Order = 0 }]
        });

        await repo.SetThemeIdAsync(created.Id, themeId);

        var reloaded = await repo.GetByIdAsync(created.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(themeId, reloaded!.ThemeId);
        Assert.Equal("Amazing Grace", reloaded.Title);
        Assert.Equal("John Newton", reloaded.Author);
        Assert.Single(reloaded.Sections);
        Assert.Equal("Line one", reloaded.Sections[0].Lyrics);
    }

    [Fact]
    public async Task SetThemeIdAsync_Null_ClearsTheme()
    {
        var repo = new SongRepository(_factory);
        var themeId = await SeedThemeAsync();
        var created = await repo.AddAsync(new Song { Title = "Song", Sections = [] });
        await repo.SetThemeIdAsync(created.Id, themeId);

        await repo.SetThemeIdAsync(created.Id, themeId: null);

        var reloaded = await repo.GetByIdAsync(created.Id);
        Assert.Null(reloaded!.ThemeId);
    }

    [Fact]
    public async Task SetThemeIdAsync_UnknownSong_Throws()
    {
        var repo = new SongRepository(_factory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SetThemeIdAsync(999, themeId: 1));
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
