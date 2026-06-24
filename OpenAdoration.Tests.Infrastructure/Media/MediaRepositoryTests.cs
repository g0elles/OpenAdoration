using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Media;

/// <summary>
/// Phase 1 of the background-library feature: background and general media are an exclusive
/// category. <see cref="MediaRepository.GetAllAsync"/> returns general only,
/// <see cref="MediaRepository.GetBackgroundsAsync"/> backgrounds only, and content-hash dedup is
/// scoped per-category (the same bytes can exist once as a background and once as general media).
/// </summary>
public sealed class MediaRepositoryTests : IDisposable
{
    private readonly SqliteFactory _factory = new();
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs = [];

    [Fact]
    public async Task BackgroundsAreExclusiveCategory_WithPerCategoryDedup()
    {
        var repo = new MediaRepository(_factory);
        const string sharedHash = "DEADBEEF";

        await repo.AddAsync(NewFile("slide.png", sharedHash, isBackground: false));
        await repo.AddAsync(NewFile("bg.png", sharedHash, isBackground: true));

        var general     = await repo.GetAllAsync();
        var backgrounds = await repo.GetBackgroundsAsync();

        Assert.Equal("slide.png", Assert.Single(general).FileName);
        Assert.Equal("bg.png",    Assert.Single(backgrounds).FileName);

        // Same hash, two categories: each lookup must return its own record, not the other's.
        Assert.False((await repo.GetByContentHashAsync(sharedHash, isBackground: false))!.IsBackground);
        Assert.True((await repo.GetByContentHashAsync(sharedHash, isBackground: true))!.IsBackground);
    }

    [Fact]
    public async Task ImportBackgroundAsync_CopiesIntoStore_AndDedupsByContent()
    {
        var store = Directory.CreateTempSubdirectory("oabg_");
        _tempDirs.Add(store.FullName);

        var repo    = new MediaRepository(_factory);
        var service = new MediaService(repo, new AppPaths("x.db", store.FullName, "s.json"), NullLogger<MediaService>.Instance);

        var src = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}.png");
        File.WriteAllText(src, "image-bytes");
        _tempFiles.Add(src);

        var first = await service.ImportBackgroundAsync(src);
        Assert.True(first.IsBackground);
        Assert.StartsWith(store.FullName, first.FilePath); // copied into the managed store
        Assert.True(File.Exists(first.FilePath));

        var second = await service.ImportBackgroundAsync(src); // identical bytes
        Assert.Equal(first.Id, second.Id);                     // reused, not duplicated
        Assert.Single(await repo.GetBackgroundsAsync());
    }

    private MediaFile NewFile(string name, string hash, bool isBackground)
    {
        // AddAsync guards File.Exists, so back each record with a real temp file.
        var path = Path.Combine(Path.GetTempPath(), $"mrt_{Guid.NewGuid():N}_{name}");
        File.WriteAllText(path, "x");
        _tempFiles.Add(path);
        return new MediaFile { FileName = name, FilePath = path, Type = MediaType.Image, ContentHash = hash, IsBackground = isBackground };
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        foreach (var d in _tempDirs)
            if (Directory.Exists(d)) Directory.Delete(d, recursive: true);
    }

    /// <summary>In-memory SQLite context factory; one shared open connection keeps the schema alive.</summary>
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
