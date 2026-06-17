using System.IO.Compression;
using System.Text.Json;
using OpenAdoration.Application.Common;

namespace OpenAdoration.Infrastructure.Backup;

/// <summary>
/// Pure <c>.oabak</c> (zip) packing/unpacking + the restore-compatibility decision —
/// no EF or live-connection dependency, so it is unit-testable on its own. The
/// <see cref="ZipBackupService"/> adds the database snapshot and migration lookup around it.
/// </summary>
public static class BackupArchive
{
    public const string ManifestEntry = "manifest.json";
    public const string DbEntry       = "openadoration.db";
    public const string SettingsEntry = "settings.json";
    public const string MediaPrefix   = "Media/";

    // Uncompressed:compressed zip-bomb guard, mirroring the Bible/media importers.
    private const long MaxCompressionRatio = 50;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static void Pack(
        string destinationOabak, string dbSnapshotPath, string? settingsPath, string mediaDir, BackupManifest manifest)
    {
        using var fs = File.Create(destinationOabak);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        using (var writer = new StreamWriter(zip.CreateEntry(ManifestEntry).Open()))
            writer.Write(JsonSerializer.Serialize(manifest, JsonOpts));

        zip.CreateEntryFromFile(dbSnapshotPath, DbEntry);

        if (settingsPath is not null && File.Exists(settingsPath))
            zip.CreateEntryFromFile(settingsPath, SettingsEntry);

        if (Directory.Exists(mediaDir))
            foreach (var file in Directory.EnumerateFiles(mediaDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(mediaDir, file).Replace('\\', '/');
                zip.CreateEntryFromFile(file, MediaPrefix + rel);
            }
    }

    public static BackupManifest? ReadManifest(ZipArchive zip)
    {
        var entry = zip.GetEntry(ManifestEntry);
        if (entry is null) return null;
        try
        {
            using var stream = entry.Open();
            return JsonSerializer.Deserialize<BackupManifest>(stream, JsonOpts);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>A backup is restorable only if this app knows its schema migration.</summary>
    public static RestoreOutcome Evaluate(BackupManifest? manifest, IReadOnlyCollection<string> knownMigrations)
    {
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.MigrationId))
            return RestoreOutcome.Corrupt;

        return knownMigrations.Contains(manifest.MigrationId)
            ? RestoreOutcome.Compatible
            : RestoreOutcome.NeedsNewerApp;
    }

    /// <summary>
    /// Extracts settings + media in place and the database to <paramref name="dbStagePath"/>
    /// (swapped in on next startup — the live DB can't be overwritten while open).
    /// </summary>
    public static void Unpack(ZipArchive zip, string dbStagePath, string settingsPath, string mediaDir)
    {
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName == ManifestEntry || entry.Name.Length == 0) continue; // skip manifest + dir entries

            if (IsCompressionRatioSuspicious(entry.Length, entry.CompressedLength))
                throw new InvalidDataException($"Backup entry '{entry.FullName}' has a suspicious compression ratio.");

            if (entry.FullName == DbEntry)
                ExtractTo(entry, dbStagePath);
            else if (entry.FullName == SettingsEntry)
                ExtractTo(entry, settingsPath);
            else if (entry.FullName.StartsWith(MediaPrefix, StringComparison.Ordinal))
                ExtractTo(entry, SafeCombine(mediaDir, entry.FullName[MediaPrefix.Length..]));
        }
    }

    private static void ExtractTo(ZipArchiveEntry entry, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        entry.ExtractToFile(destinationPath, overwrite: true);
    }

    // Reject zip-slip: the resolved path must stay inside the media directory.
    private static string SafeCombine(string root, string relative)
    {
        var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Backup entry escapes the media directory: '{relative}'.");
        return full;
    }

    private static bool IsCompressionRatioSuspicious(long uncompressed, long compressed) =>
        compressed > 0 && uncompressed / compressed > MaxCompressionRatio;
}
