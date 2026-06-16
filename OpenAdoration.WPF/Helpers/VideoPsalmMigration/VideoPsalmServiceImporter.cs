using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.VideoPsalmMigration;

/// <summary>
/// Builds a <see cref="WorshipService"/> from a VideoPsalm <c>.vpagd</c> agenda in true
/// (zip) order: songs (dedup by <see cref="Song.SourceGuid"/>), scripture as <b>references
/// only</b> (verse text never harvested — licensed), media (dedup by content hash, bytes
/// extracted to the media store). Song/scripture items also get a reconstructed
/// <see cref="Theme"/> (font, color, background, scripture templates) deduped within the import.
/// Re-importing the same archive is detected and skipped. See ROADMAP.md M12.3/M12.4.
/// </summary>
public sealed class VideoPsalmServiceImporter
{
    private static readonly string MediaStore = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAdoration", "Media");

    private static readonly string ServiceArchiveStore = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAdoration", "Services");

    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".m4v" };

    private readonly ISongService _songs;
    private readonly IMediaService _media;
    private readonly IWorshipServiceService _services;
    private readonly IBibleService _bible;
    private readonly IThemeService _themes;
    private readonly ILogger<VideoPsalmServiceImporter> _logger;

    public VideoPsalmServiceImporter(
        ISongService songs, IMediaService media, IWorshipServiceService services,
        IBibleService bible, IThemeService themes, ILogger<VideoPsalmServiceImporter> logger)
    {
        _songs = songs;
        _media = media;
        _services = services;
        _bible = bible;
        _themes = themes;
        _logger = logger;
    }

    public async Task<VpImportSummary> ImportAsync(string filePath, CancellationToken ct = default)
    {
        var agenda = VideoPsalmAgendaParser.Parse(filePath);
        var sourceGuid = ComputeFileHash(filePath);

        var existing = await _services.GetBySourceGuidAsync(sourceGuid, ct);
        if (existing is not null)
        {
            _logger.LogInformation("VideoPsalm agenda already imported as service {ServiceId}", existing.Id);
            return new VpImportSummary { ServiceName = existing.Name, AlreadyImported = true, TotalItems = agenda.Items.Count };
        }

        var service = await _services.CreateAsync(new WorshipService
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Date = DateTime.Today,
            SourceGuid = sourceGuid,
            SourceArchivePath = ArchiveOriginal(filePath)
        }, ct);

        var ctx = new ImportContext
        {
            FilePath = filePath,
            ServiceName = service.Name,
            Versions = await BuildVersionLookupAsync(ct)
        };

        foreach (var item in agenda.Items)
        {
            ct.ThrowIfCancellationRequested();
            await ImportItemAsync(service.Id, item, ctx, ct);
        }

        _logger.LogInformation("Imported VideoPsalm service '{Name}' ({Items} items)", service.Name, agenda.Items.Count);
        return ctx.Counters.ToSummary(service.Name, agenda.Items.Count);
    }

    private async Task ImportItemAsync(int serviceId, VpAgendaItem item, ImportContext ctx, CancellationToken ct)
    {
        var autoAdvance = item.Properties.AutoAdvanceSeconds;
        switch (item.Type)
        {
            case VpItemType.Song: await ImportSongAsync(serviceId, item, autoAdvance, ctx, ct); break;
            case VpItemType.Scripture: await ImportScriptureAsync(serviceId, item, autoAdvance, ctx, ct); break;
            case VpItemType.Image:
            case VpItemType.Video: await ImportMediaAsync(serviceId, item, autoAdvance, ctx, ct); break;
        }
    }

    private async Task ImportSongAsync(int serviceId, VpAgendaItem item, int? autoAdvance, ImportContext ctx, CancellationToken ct)
    {
        if (item.Song is null) { ctx.Counters.ItemsSkipped++; return; }

        var existing = item.Song.SourceGuid is { } guid ? await _songs.GetBySourceGuidAsync(guid, ct) : null;
        int songId;
        if (existing is not null) { songId = existing.Id; ctx.Counters.SongsReused++; }
        else { songId = (await _songs.CreateAsync(item.Song, ct)).Id; ctx.Counters.SongsImported++; }

        // Song templates in VP are operator scratch text, not tokens — skip them; keep font + background.
        var themeId = await ResolveThemeAsync(item.Style, includeTemplates: false, "Songs", ctx, ct);
        await _services.AddSongItemAsync(serviceId, songId, themeId, autoAdvance, ct);
    }

    private async Task ImportScriptureAsync(int serviceId, VpAgendaItem item, int? autoAdvance, ImportContext ctx, CancellationToken ct)
    {
        var s = item.Scripture!;
        int? versionId = ctx.Versions.TryGetValue(s.VersionAbbreviation.ToUpperInvariant(), out var id) ? id : null;

        // Reference only — verse text is licensed and intentionally not stored; it resolves
        // at projection time from whatever version the church has legally installed.
        var themeId = await ResolveThemeAsync(item.Style, includeTemplates: true, "Scripture", ctx, ct);
        await _services.AddBibleItemAsync(
            serviceId, s.BookName, s.Chapter, s.VerseStart, s.VerseEnd, versionId, themeId, autoAdvance, ct);
        ctx.Counters.ScriptureReferences++;
    }

    private async Task ImportMediaAsync(int serviceId, VpAgendaItem item, int? autoAdvance, ImportContext ctx, CancellationToken ct)
    {
        if (item.MediaEntryName is null) { ctx.Counters.MediaMissing++; return; }

        var resolved = await ResolveMediaAsync(ctx.FilePath, item.MediaEntryName, ct);
        if (resolved is null) { ctx.Counters.MediaMissing++; return; }

        if (resolved.Value.Reused) ctx.Counters.MediaReused++; else ctx.Counters.MediaImported++;
        await _services.AddMediaItemAsync(serviceId, resolved.Value.MediaId, autoAdvanceSeconds: autoAdvance, ct: ct);
    }

    // ── Themes ────────────────────────────────────────────────────────────────

    private async Task<int?> ResolveThemeAsync(VpStyle? style, bool includeTemplates, string label, ImportContext ctx, CancellationToken ct)
    {
        if (style is null) return null;

        var header = includeTemplates ? style.HeaderTemplate : null;
        var footer = includeTemplates ? style.FooterTemplate : null;
        var signature = string.Join("|", label, style.FontFamily, style.FontColor, header, footer, style.BackgroundImage, style.BackgroundVideo);
        if (ctx.ThemeCache.TryGetValue(signature, out var cached)) return cached;

        var defaults = new Theme();
        var videoPath = ExtractBackground(ctx, style.BackgroundVideo);
        var imagePath = videoPath is null ? ExtractBackground(ctx, style.BackgroundImage) : null;

        var theme = await _themes.CreateAsync(new Theme
        {
            Name = $"{ctx.ServiceName} · VideoPsalm {label}",
            FontFamily = style.FontFamily ?? defaults.FontFamily,
            FontColor = style.FontColor ?? defaults.FontColor,
            HeaderTemplate = header,
            FooterTemplate = footer,
            BackgroundImagePath = imagePath,
            BackgroundVideoPath = videoPath
        }, ct);

        ctx.Counters.ThemesCreated++;
        ctx.ThemeCache[signature] = theme.Id;
        return theme.Id;
    }

    /// <summary>Extracts a style background (image/video) by basename to the media store; cached per import.</summary>
    private static string? ExtractBackground(ImportContext ctx, string? basename)
    {
        if (basename is null) return null;
        if (ctx.BackgroundCache.TryGetValue(basename, out var existing)) return existing;

        using var archive = ZipFile.OpenRead(ctx.FilePath);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), basename, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        var path = ExtractToStore(entry);
        ctx.BackgroundCache[basename] = path;
        return path;
    }

    // ── Media ───────────────────────────────────────────────────────────────

    private async Task<(int MediaId, bool Reused)?> ResolveMediaAsync(string filePath, string entryName, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var entry = archive.GetEntry(entryName);
        if (entry is null) return null;

        var hash = HashEntry(entry);
        var existing = await _media.GetByContentHashAsync(hash, ct);
        if (existing is not null) return (existing.Id, true);

        var destPath = ExtractToStore(entry);
        var media = await _media.AddAsync(new MediaFile
        {
            FileName = Path.GetFileName(destPath),
            FilePath = destPath,
            Type = VideoExtensions.Contains(Path.GetExtension(destPath)) ? MediaType.Video : MediaType.Image,
            ContentHash = hash
        }, ct);

        return (media.Id, false);
    }

    private static string ExtractToStore(ZipArchiveEntry entry)
    {
        Directory.CreateDirectory(MediaStore);
        var destPath = UniquePath(MediaStore, Path.GetFileName(entry.FullName));
        using var source = entry.Open();
        using var dest = File.Create(destPath);
        source.CopyTo(dest);
        return destPath;
    }

    private static string ArchiveOriginal(string filePath)
    {
        Directory.CreateDirectory(ServiceArchiveStore);
        var destPath = UniquePath(ServiceArchiveStore, Path.GetFileName(filePath));
        File.Copy(filePath, destPath);
        return destPath;
    }

    private async Task<IReadOnlyDictionary<string, int>> BuildVersionLookupAsync(CancellationToken ct)
    {
        var versions = await _bible.GetVersionsAsync(ct);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in versions)
            map[v.Abbreviation.ToUpperInvariant()] = v.Id;
        return map;
    }

    private static string HashEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string UniquePath(string directory, string fileName)
    {
        var destPath = Path.Combine(directory, fileName);
        if (!File.Exists(destPath)) return destPath;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var n = 1;
        do { destPath = Path.Combine(directory, $"{name} ({n++}){ext}"); }
        while (File.Exists(destPath));
        return destPath;
    }

    /// <summary>Mutable per-import state (sequential — no concurrency).</summary>
    private sealed class ImportContext
    {
        public required string FilePath { get; init; }
        public required string ServiceName { get; init; }
        public required IReadOnlyDictionary<string, int> Versions { get; init; }
        public Counters Counters { get; } = new();
        public Dictionary<string, int> ThemeCache { get; } = new();
        public Dictionary<string, string> BackgroundCache { get; } = new();
    }

    private sealed class Counters
    {
        public int SongsImported, SongsReused, ScriptureReferences;
        public int MediaImported, MediaReused, MediaMissing, ItemsSkipped, ThemesCreated;

        public VpImportSummary ToSummary(string serviceName, int totalItems) => new()
        {
            ServiceName = serviceName,
            SongsImported = SongsImported,
            SongsReused = SongsReused,
            ScriptureReferences = ScriptureReferences,
            MediaImported = MediaImported,
            MediaReused = MediaReused,
            MediaMissing = MediaMissing,
            ItemsSkipped = ItemsSkipped,
            ThemesCreated = ThemesCreated,
            TotalItems = totalItems
        };
    }
}
