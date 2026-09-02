using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Songs;

/// <summary>
/// F4: VideoPsalm songbook/agenda songs carry a stable <see cref="Song.SourceGuid"/>, so a
/// re-import (or an item shared between a songbook and an agenda) must reuse the existing row
/// instead of creating a duplicate. Formats without a SourceGuid (Word, plain text, ChordPro,
/// OpenLyrics/OpenSong) always create — there is nothing to dedup against.
/// </summary>
public sealed class SongServiceCreateOrReuseTests : IDisposable
{
    private readonly SqliteFactory _factory = new();

    private SongService Service() =>
        new(new SongRepository(_factory), NullLogger<SongService>.Instance);

    private static Song NewSong(string title, string? sourceGuid) => new()
    {
        Title = title,
        SourceGuid = sourceGuid,
        Sections = [new SongSection { Type = SectionType.Verse, SectionNumber = 1, Lyrics = "x", Order = 0 }]
    };

    [Fact]
    public async Task CreateOrReuseAsync_NoSourceGuid_AlwaysCreates()
    {
        var service = Service();

        var (first, firstReused) = await service.CreateOrReuseAsync(NewSong("Song A", null));
        var (second, secondReused) = await service.CreateOrReuseAsync(NewSong("Song A", null));

        Assert.False(firstReused);
        Assert.False(secondReused);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateOrReuseAsync_NewSourceGuid_Creates()
    {
        var service = Service();

        var (song, wasReused) = await service.CreateOrReuseAsync(NewSong("Song B", "guid-1"));

        Assert.False(wasReused);
        Assert.True(song.Id > 0);
    }

    [Fact]
    public async Task CreateOrReuseAsync_DuplicateSourceGuid_ReturnsExistingInstead()
    {
        var service = Service();
        var (created, _) = await service.CreateOrReuseAsync(NewSong("Song C", "guid-2"));

        var (reused, wasReused) = await service.CreateOrReuseAsync(NewSong("Song C (re-import)", "guid-2"));

        Assert.True(wasReused);
        Assert.Equal(created.Id, reused.Id);
        Assert.Equal("Song C", reused.Title); // the original, not the re-imported title
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
