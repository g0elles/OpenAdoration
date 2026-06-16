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
/// extracted to the media store). Re-importing the same archive is detected and skipped.
/// See ROADMAP.md M12.3.
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
    private readonly ILogger<VideoPsalmServiceImporter> _logger;

    public VideoPsalmServiceImporter(
        ISongService songs, IMediaService media, IWorshipServiceService services,
        IBibleService bible, ILogger<VideoPsalmServiceImporter> logger)
    {
        _songs = songs;
        _media = media;
        _services = services;
        _bible = bible;
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

        var versionsByAbbreviation = await BuildVersionLookupAsync(ct);
        var counters = new Counters();

        foreach (var item in agenda.Items)
        {
            ct.ThrowIfCancellationRequested();
            await ImportItemAsync(service.Id, filePath, item, versionsByAbbreviation, counters, ct);
        }

        _logger.LogInformation("Imported VideoPsalm service '{Name}' ({Items} items)", service.Name, agenda.Items.Count);
        return counters.ToSummary(service.Name, agenda.Items.Count);
    }

    private async Task ImportItemAsync(
        int serviceId, string filePath, VpAgendaItem item,
        IReadOnlyDictionary<string, int> versions, Counters counters, CancellationToken ct)
    {
        var autoAdvance = item.Properties.AutoAdvanceSeconds;
        switch (item.Type)
        {
            case VpItemType.Song: await ImportSongAsync(serviceId, item, autoAdvance, counters, ct); break;
            case VpItemType.Scripture: await ImportScriptureAsync(serviceId, item, versions, autoAdvance, counters, ct); break;
            case VpItemType.Image:
            case VpItemType.Video: await ImportMediaAsync(serviceId, filePath, item, autoAdvance, counters, ct); break;
        }
    }

    private async Task ImportSongAsync(
        int serviceId, VpAgendaItem item, int? autoAdvance, Counters counters, CancellationToken ct)
    {
        if (item.Song is null) { counters.ItemsSkipped++; return; }

        var existing = item.Song.SourceGuid is { } guid ? await _songs.GetBySourceGuidAsync(guid, ct) : null;
        int songId;
        if (existing is not null) { songId = existing.Id; counters.SongsReused++; }
        else { songId = (await _songs.CreateAsync(item.Song, ct)).Id; counters.SongsImported++; }

        await _services.AddSongItemAsync(serviceId, songId, autoAdvanceSeconds: autoAdvance, ct: ct);
    }

    private async Task ImportScriptureAsync(
        int serviceId, VpAgendaItem item, IReadOnlyDictionary<string, int> versions,
        int? autoAdvance, Counters counters, CancellationToken ct)
    {
        var s = item.Scripture!;
        int? versionId = versions.TryGetValue(s.VersionAbbreviation.ToUpperInvariant(), out var id) ? id : null;

        // Reference only — verse text is licensed and intentionally not stored; it resolves
        // at projection time from whatever version the church has legally installed.
        await _services.AddBibleItemAsync(
            serviceId, s.BookName, s.Chapter, s.VerseStart, s.VerseEnd, versionId, autoAdvanceSeconds: autoAdvance, ct: ct);
        counters.ScriptureReferences++;
    }

    private async Task ImportMediaAsync(
        int serviceId, string filePath, VpAgendaItem item, int? autoAdvance, Counters counters, CancellationToken ct)
    {
        if (item.MediaEntryName is null) { counters.MediaMissing++; return; }

        var resolved = await ResolveMediaAsync(filePath, item.MediaEntryName, ct);
        if (resolved is null) { counters.MediaMissing++; return; }

        if (resolved.Value.Reused) counters.MediaReused++; else counters.MediaImported++;
        await _services.AddMediaItemAsync(serviceId, resolved.Value.MediaId, autoAdvanceSeconds: autoAdvance, ct: ct);
    }

    private async Task<(int MediaId, bool Reused)?> ResolveMediaAsync(
        string filePath, string entryName, CancellationToken ct)
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

    private sealed class Counters
    {
        public int SongsImported, SongsReused, ScriptureReferences;
        public int MediaImported, MediaReused, MediaMissing, ItemsSkipped;

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
            TotalItems = totalItems
        };
    }
}
