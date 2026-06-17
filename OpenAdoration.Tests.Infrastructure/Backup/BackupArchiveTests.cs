using System.IO.Compression;
using OpenAdoration.Application.Common;
using OpenAdoration.Infrastructure.Backup;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Backup;

/// <summary>
/// Covers the data-loss-sensitive logic of <see cref="BackupArchive"/>: the restore
/// compatibility gate and a full pack → unpack round-trip (DB + settings + nested media).
/// The EF migration lookup and live-DB snapshot in <c>ZipBackupService</c> are GUI-tested.
/// </summary>
public sealed class BackupArchiveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oabak-test-" + Guid.NewGuid().ToString("N"));

    public BackupArchiveTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private static BackupManifest Manifest(string migrationId) =>
        new() { AppVersion = "1.2.3", CreatedAtUtc = DateTime.UtcNow, MigrationId = migrationId };

    [Fact]
    public void Evaluate_NullOrEmptyManifest_IsCorrupt()
    {
        Assert.Equal(RestoreOutcome.Corrupt, BackupArchive.Evaluate(null, ["m1"]));
        Assert.Equal(RestoreOutcome.Corrupt, BackupArchive.Evaluate(Manifest("  "), ["m1"]));
    }

    [Fact]
    public void Evaluate_UnknownMigration_NeedsNewerApp()
        => Assert.Equal(RestoreOutcome.NeedsNewerApp, BackupArchive.Evaluate(Manifest("m9_future"), ["m1", "m2"]));

    [Fact]
    public void Evaluate_KnownMigration_IsCompatible()
        => Assert.Equal(RestoreOutcome.Compatible, BackupArchive.Evaluate(Manifest("m2"), ["m1", "m2"]));

    [Fact]
    public void PackThenUnpack_RestoresDbSettingsAndNestedMedia()
    {
        // ── source library ───────────────────────────────────────────────
        var srcDb       = Path.Combine(_root, "openadoration.db");
        var srcSettings = Path.Combine(_root, "settings.json");
        var srcMedia    = Path.Combine(_root, "Media");
        Directory.CreateDirectory(Path.Combine(srcMedia, "sub"));
        File.WriteAllText(srcDb, "fake-sqlite-bytes");
        File.WriteAllText(srcSettings, "{\"ChurchName\":\"Grace\"}");
        File.WriteAllText(Path.Combine(srcMedia, "a.png"), "image-a");
        File.WriteAllText(Path.Combine(srcMedia, "sub", "b.mp4"), "video-b");

        var oabak = Path.Combine(_root, "backup.oabak");
        var manifest = Manifest("20260616211931_AddVideoPsalmMigrationFields");
        BackupArchive.Pack(oabak, srcDb, srcSettings, srcMedia, manifest);

        // ── restore target ──────────────────────────────────────────────
        var destDb       = Path.Combine(_root, "restore", "openadoration.db.restore");
        var destSettings = Path.Combine(_root, "restore", "settings.json");
        var destMedia    = Path.Combine(_root, "restore", "Media");

        using (var zip = ZipFile.OpenRead(oabak))
        {
            var read = BackupArchive.ReadManifest(zip);
            Assert.NotNull(read);
            Assert.Equal(manifest.MigrationId, read!.MigrationId);
            Assert.Equal(manifest.AppVersion, read.AppVersion);

            BackupArchive.Unpack(zip, destDb, destSettings, destMedia);
        }

        Assert.Equal("fake-sqlite-bytes", File.ReadAllText(destDb));
        Assert.Equal("{\"ChurchName\":\"Grace\"}", File.ReadAllText(destSettings));
        Assert.Equal("image-a", File.ReadAllText(Path.Combine(destMedia, "a.png")));
        Assert.Equal("video-b", File.ReadAllText(Path.Combine(destMedia, "sub", "b.mp4")));
    }
}
