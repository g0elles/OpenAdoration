using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence;
using OpenAdoration.Infrastructure.Repositories;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Schedule;

/// <summary>F9: Notes/Sermon is a real library entity (like Song, unlike Bible) -- a schedule item
/// just points at a <see cref="Note"/> via NoteId, mirroring <see cref="SongScheduleItem"/>.</summary>
public sealed class WorshipServiceRepositoryNotesTests : IDisposable
{
    private readonly SqliteFactory _factory = new();

    private async Task<Note> SeedNoteAsync(NoteRepository noteRepo) =>
        await noteRepo.AddAsync(new Note { Title = "Sermon", Content = "Line one\n\nLine two" });

    [Fact]
    public async Task AddNotesItemAsync_PersistsNoteReference()
    {
        var repo = new WorshipServiceRepository(_factory);
        var noteRepo = new NoteRepository(_factory);
        var note = await SeedNoteAsync(noteRepo);
        var service = await repo.AddAsync(new WorshipService { Name = "Sunday Service", Date = DateTime.UtcNow });

        await repo.AddNotesItemAsync(service.Id, note.Id);

        var item = Assert.Single((await repo.GetWithItemsAsync(service.Id))!.Items);
        var notesItem = Assert.IsType<NotesScheduleItem>(item);
        Assert.Equal(note.Id, notesItem.NoteId);
        Assert.Equal("Sermon", notesItem.Note.Title);
        Assert.Equal("Line one\n\nLine two", notesItem.Note.Content);
        Assert.Equal(0, notesItem.Order);
    }

    [Fact]
    public async Task AddNotesItemAsync_SetsThemeIdAndAutoAdvance()
    {
        var repo = new WorshipServiceRepository(_factory);
        var noteRepo = new NoteRepository(_factory);
        var themeRepo = new ThemeRepository(_factory);
        var note = await SeedNoteAsync(noteRepo);
        var theme = await themeRepo.AddAsync(new Theme { Name = "Custom" });
        var service = await repo.AddAsync(new WorshipService { Name = "Sunday Service", Date = DateTime.UtcNow });

        await repo.AddNotesItemAsync(service.Id, note.Id, themeId: theme.Id, autoAdvanceSeconds: 20);

        var item = Assert.Single((await repo.GetWithItemsAsync(service.Id))!.Items);
        Assert.Equal(theme.Id, item.ThemeId);
        Assert.Equal(20, item.AutoAdvanceSeconds);
    }

    [Fact]
    public async Task AddNotesItemAsync_AppendsAfterExistingItems()
    {
        var repo = new WorshipServiceRepository(_factory);
        var songRepo = new SongRepository(_factory);
        var noteRepo = new NoteRepository(_factory);
        var song = await songRepo.AddAsync(new Song { Title = "Song", Sections = [] });
        var note = await SeedNoteAsync(noteRepo);
        var service = await repo.AddAsync(new WorshipService { Name = "Sunday Service", Date = DateTime.UtcNow });
        await repo.AddSongItemAsync(service.Id, song.Id);

        await repo.AddNotesItemAsync(service.Id, note.Id);

        var items = (await repo.GetWithItemsAsync(service.Id))!.Items.OrderBy(i => i.Order).ToList();
        Assert.Equal(2, items.Count);
        Assert.IsType<NotesScheduleItem>(items[1]);
        Assert.Equal(1, items[1].Order);
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
