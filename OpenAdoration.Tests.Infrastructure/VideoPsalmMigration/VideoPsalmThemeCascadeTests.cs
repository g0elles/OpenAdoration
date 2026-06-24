using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Helpers.VideoPsalmMigration;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.VideoPsalmMigration;

/// <summary>
/// M14.4: VideoPsalm import assigns themes at the right cascade level instead of per schedule item —
/// song style → <c>Song.ThemeId</c>; BibleStyle → <c>DefaultScriptureThemeId</c> (only if unset);
/// RootStyle → the app-default theme (only if never hand-edited); schedule items stay theme-null.
/// </summary>
public sealed class VideoPsalmThemeCascadeTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"vpcascade_{Guid.NewGuid():N}.vpagd");

    // One song (root font Candara, own video bg) + one scripture (BibleStyle templates). No media.
    private static readonly (string Name, string Content)[] Agenda =
    [
        ("Version.json", "2"),
        ("RootStyle.json", "{Body:{FontName:\"Candara\",FontStyle:{Fill:{Color:\"FFFFFFFF\"}}},Background:{Image:\"bg-root.png\"}}"),
        ("SongBookStyle.json", "{Header:{Template:\"scratch\"}}"),
        ("BibleStyle.json", "{Header:{Template:\"[BibleBookName] [BibleChapterID]\"},Footer:{Template:\"[BibleDescription]\"}}"),
        ("Song_0.json", "{Guid:\"song-guid-1\",Style:{Background:{Video:\"song-bg.wmv\"}},Verses:[{\nText:\"Line one\"}],\nText:\"Mi Canción\"}"),
        ("SongBook_0.json", "{Text:\"SONG\",Guid:\"book\"}"),
        ("BibleVerses_0.json", "{Verses:[{\nText:\"v1\"}]}"),
        ("BibleChapter_0.json", "{ID:7,Verses:[]}"),
        ("BibleBook_0.json", "{ID:5,Text:\"Josué\",Abbreviation:\"Jos\",Chapters:[]}"),
        ("Bible_0.json", "{Abbreviation:\"NVI-S\",Language:\"es\",Text:\"NVI\",Testaments:[]}"),
        ("AgendaItemProperties.json", "{Items:[{AutoAdvance:0,Interval:0},{AutoAdvance:0,Interval:0}]}"),
    ];

    private void BuildAgenda()
    {
        using var stream = File.Create(_path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in Agenda)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }
    }

    private static VideoPsalmServiceImporter Importer(FakeBackend b) =>
        new(b, b, b, b, b, b, NullLogger<VideoPsalmServiceImporter>.Instance);

    [Fact]
    public async Task Import_RoutesStylesToCascadeLevels_NotToScheduleItems()
    {
        BuildAgenda();
        var backend = new FakeBackend { DefaultThemePristine = true }; // fresh install, no defaults set

        await Importer(backend).ImportAsync(_path);

        // Song style → the song's own ThemeId (font carried down from RootStyle), schedule item theme-null.
        Assert.NotNull(backend.CreatedSong);
        Assert.NotNull(backend.CreatedSong!.ThemeId);
        Assert.Equal("Candara", backend.ThemeById(backend.CreatedSong.ThemeId!.Value).FontFamily);
        Assert.Null(backend.SongItemThemeId);

        // BibleStyle → Scripture content-type default (was null), schedule item theme-null.
        Assert.NotNull(backend.Settings.DefaultScriptureThemeId);
        Assert.Null(backend.BibleItemThemeId);

        // RootStyle → app-default theme (default was pristine).
        Assert.NotNull(backend.SetDefaultThemeId);
    }

    [Fact]
    public async Task Import_NeverClobbersExistingDefaults()
    {
        BuildAgenda();
        var backend = new FakeBackend
        {
            DefaultThemePristine = false,           // operator has hand-edited the default theme
            Settings = { DefaultScriptureThemeId = 42 } // and already chose a scripture default
        };

        await Importer(backend).ImportAsync(_path);

        Assert.Equal(42, backend.Settings.DefaultScriptureThemeId); // untouched
        Assert.Null(backend.SetDefaultThemeId);                     // app default left alone
        Assert.NotNull(backend.CreatedSong!.ThemeId);               // songs are still themed (new song)
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        // The importer copies the agenda into %LOCALAPPDATA%\OpenAdoration\Services — clean our copy.
        var store = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAdoration", "Services");
        if (Directory.Exists(store))
            foreach (var f in Directory.GetFiles(store, Path.GetFileNameWithoutExtension(_path) + "*"))
                File.Delete(f);
    }

    /// <summary>One fake standing in for every service the importer touches; captures the routing decisions.</summary>
    private sealed class FakeBackend
        : ISongService, IMediaService, IWorshipServiceService, IBibleService, IThemeService, IAppSettingsService
    {
        private readonly List<Theme> _themes = [];
        private int _nextThemeId = 100;

        public Song? CreatedSong;
        public int? SongItemThemeId = -1;   // sentinel: -1 = AddSongItem never called
        public int? BibleItemThemeId = -1;
        public int? SetDefaultThemeId;
        public bool DefaultThemePristine = true;
        public AppSettings Settings { get; } = new();
        public AppSettings Current => Settings;

        public Theme ThemeById(int id) => _themes.Single(t => t.Id == id);

        // ── IThemeService ──
        public Task<Theme> CreateAsync(Theme theme, CancellationToken ct = default)
        {
            theme.Id = _nextThemeId++;
            _themes.Add(theme);
            return Task.FromResult(theme);
        }
        public Task<Theme> GetDefaultAsync(CancellationToken ct = default)
        {
            var stamp = DateTime.UtcNow;
            // Pristine = never modified since creation (UpdatedAt == CreatedAt).
            return Task.FromResult(new Theme
            {
                Id = 1, Name = "Default", IsDefault = true,
                CreatedAt = stamp, UpdatedAt = DefaultThemePristine ? stamp : stamp.AddMinutes(5)
            });
        }
        public Task SetDefaultAsync(int id, CancellationToken ct = default) { SetDefaultThemeId = id; return Task.CompletedTask; }

        // ── IAppSettingsService ──
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task FlushAsync() => Task.CompletedTask;

        // ── ISongService ──
        public Task<Song?> GetBySourceGuidAsync(string sourceGuid, CancellationToken ct = default) => Task.FromResult<Song?>(null);
        Task<Song> ISongService.CreateAsync(Song song, CancellationToken ct)
        {
            CreatedSong = song;
            song.Id = 7;
            return Task.FromResult(song);
        }

        // ── IWorshipServiceService ──
        Task<WorshipService?> IWorshipServiceService.GetBySourceGuidAsync(string sourceGuid, CancellationToken ct) => Task.FromResult<WorshipService?>(null);
        Task<WorshipService> IWorshipServiceService.CreateAsync(WorshipService service, CancellationToken ct)
        {
            service.Id = 1;
            return Task.FromResult(service);
        }
        public Task AddSongItemAsync(int serviceId, int songId, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default)
        { SongItemThemeId = themeId; return Task.CompletedTask; }
        public Task AddBibleItemAsync(int serviceId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId = null, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default)
        { BibleItemThemeId = themeId; return Task.CompletedTask; }

        // ── IBibleService ──
        public Task<IReadOnlyList<BibleVersion>> GetVersionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BibleVersion>>([]);

        // ── Everything else the importer doesn't reach ──
        Task<Song?> ISongService.GetByIdAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Song>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Song>> SearchByTitleAsync(string term, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Song>> SearchByLyricsAsync(string term, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Song song, CancellationToken ct = default) => throw new NotImplementedException();
        Task ISongService.DeleteAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public IReadOnlyList<Slide> GenerateSlides(Song song, int? themeId = null, string? verseOrderOverride = null) => throw new NotImplementedException();

        Task<IReadOnlyList<MediaFile>> IMediaService.GetAllAsync(CancellationToken ct) => throw new NotImplementedException();
        Task<IReadOnlyList<MediaFile>> IMediaService.GetBackgroundsAsync(CancellationToken ct) => throw new NotImplementedException();
        Task<MediaFile?> IMediaService.GetByIdAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<MediaFile?> GetByContentHashAsync(string contentHash, bool isBackground = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MediaFile> ImportBackgroundAsync(string sourcePath, CancellationToken ct = default) => throw new NotImplementedException();
        Task IMediaService.DeleteAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Slide GenerateSlide(MediaFile file, int? themeId = null) => throw new NotImplementedException();

        Task<IReadOnlyList<WorshipService>> IWorshipServiceService.GetAllAsync(CancellationToken ct) => throw new NotImplementedException();
        Task<WorshipService?> IWorshipServiceService.GetByIdAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<WorshipService?> GetWithItemsAsync(int serviceId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(WorshipService service, CancellationToken ct = default) => throw new NotImplementedException();
        Task IWorshipServiceService.DeleteAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task AddMediaItemAsync(int serviceId, int mediaFileId, int? themeId = null, int? autoAdvanceSeconds = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RemoveItemAsync(int scheduleItemId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReorderItemsAsync(int serviceId, IReadOnlyList<int> orderedItemIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetItemAutoAdvanceAsync(int itemId, int? autoAdvanceSeconds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetItemVerseOrderOverrideAsync(int itemId, string? verseOrderOverride, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateBibleItemAsync(int itemId, string book, int chapter, int verseStart, int verseEnd, int? bibleVersionId, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<BibleBook>> GetBooksAsync(int versionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int versionId, string book, int chapter, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BibleVerse?> GetVerseAsync(int versionId, string book, int chapter, int verse, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BibleVerse>> SearchAsync(int versionId, string term, BibleSearchMode mode = BibleSearchMode.Keyword, int maxResults = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteVersionAsync(int versionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> DeleteVersionsBySourceAsync(string sourcePluginId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpsertVersionVersesAsync(BibleVersion version, IReadOnlyList<BibleBook> books, IReadOnlyList<BibleVerse> verses, IProgress<int>? progress = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Slide GenerateSlide(IReadOnlyList<BibleVerse> verses, int? themeId = null, BibleVersion? version = null) => throw new NotImplementedException();
        public IReadOnlyList<Slide> GenerateSlides(IReadOnlyList<BibleVerse> verses, int versesPerSlide, int? themeId = null, BibleVersion? version = null) => throw new NotImplementedException();

        Task<IReadOnlyList<Theme>> IThemeService.GetAllAsync(CancellationToken ct) => throw new NotImplementedException();
        Task<Theme?> IThemeService.GetByIdAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        Task IThemeService.UpdateAsync(Theme theme, CancellationToken ct) => throw new NotImplementedException();
        Task IThemeService.DeleteAsync(int id, CancellationToken ct) => throw new NotImplementedException();
    }
}
