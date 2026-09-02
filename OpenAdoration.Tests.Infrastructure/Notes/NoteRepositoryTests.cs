using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Notes;

/// <summary>F9 rework: Notes is a real library entity with full CRUD, like Song -- these mirror
/// the equivalent Song repository coverage.</summary>
public sealed class NoteRepositoryTests : IDisposable
{
    private readonly SqliteFactory _factory = new();

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrips()
    {
        var repo = new NoteRepository(_factory);

        var created = await repo.AddAsync(new Note { Title = "Sermon Notes", Content = "First point.\n\nSecond point." });

        var reloaded = await repo.GetByIdAsync(created.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Sermon Notes", reloaded!.Title);
        Assert.Equal("First point.\n\nSecond point.", reloaded.Content);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByTitle()
    {
        var repo = new NoteRepository(_factory);
        await repo.AddAsync(new Note { Title = "Zebra", Content = "Z" });
        await repo.AddAsync(new Note { Title = "Alpha", Content = "A" });

        var all = await repo.GetAllAsync();

        Assert.Equal(["Alpha", "Zebra"], all.Select(n => n.Title));
    }

    [Fact]
    public async Task SearchByTitleAsync_MatchesSubstring()
    {
        var repo = new NoteRepository(_factory);
        await repo.AddAsync(new Note { Title = "Sunday Sermon", Content = "Content" });
        await repo.AddAsync(new Note { Title = "Announcement", Content = "Content" });

        var results = await repo.SearchByTitleAsync("Sermon");

        var result = Assert.Single(results);
        Assert.Equal("Sunday Sermon", result.Title);
    }

    [Fact]
    public async Task UpdateAsync_PatchesTitleAndContent()
    {
        var repo = new NoteRepository(_factory);
        var created = await repo.AddAsync(new Note { Title = "Original", Content = "Original content" });

        created.Title = "Updated";
        created.Content = "Updated content";
        await repo.UpdateAsync(created);

        var reloaded = await repo.GetByIdAsync(created.Id);
        Assert.Equal("Updated", reloaded!.Title);
        Assert.Equal("Updated content", reloaded.Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheNote()
    {
        var repo = new NoteRepository(_factory);
        var created = await repo.AddAsync(new Note { Title = "Temp", Content = "Temp" });

        await repo.DeleteAsync(created.Id);

        Assert.Null(await repo.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task SetThemeIdAsync_PatchesOnlyThemeId_LeavesTitleAndContentUntouched()
    {
        var repo = new NoteRepository(_factory);
        var themeId = (await new ThemeRepository(_factory).AddAsync(new Theme { Name = "Custom" })).Id;
        var created = await repo.AddAsync(new Note { Title = "Sermon", Content = "Content" });

        await repo.SetThemeIdAsync(created.Id, themeId);

        var reloaded = await repo.GetByIdAsync(created.Id);
        Assert.Equal(themeId, reloaded!.ThemeId);
        Assert.Equal("Sermon", reloaded.Title);
        Assert.Equal("Content", reloaded.Content);
    }

    [Fact]
    public async Task SetThemeIdAsync_Null_ClearsTheme()
    {
        var repo = new NoteRepository(_factory);
        var themeId = (await new ThemeRepository(_factory).AddAsync(new Theme { Name = "Custom" })).Id;
        var created = await repo.AddAsync(new Note { Title = "Sermon", Content = "Content" });
        await repo.SetThemeIdAsync(created.Id, themeId);

        await repo.SetThemeIdAsync(created.Id, themeId: null);

        var reloaded = await repo.GetByIdAsync(created.Id);
        Assert.Null(reloaded!.ThemeId);
    }

    [Fact]
    public async Task SetThemeIdAsync_UnknownNote_Throws()
    {
        var repo = new NoteRepository(_factory);
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
