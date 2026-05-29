using System.IO;
using System.IO.Compression;
using OpenAdoration.WPF.Helpers.BibleImport;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.BibleImport;

/// <summary>
/// Guard-rail tests for the BibleSuperSearch ZIP parser: it must reject
/// zip-bomb-style archives (too many entries, suspicious compression ratio)
/// before reading any content.
/// </summary>
public sealed class BibleSuperSearchZipGuardTests
{
    private static string CreateTempZip(Action<ZipArchive> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"oa_ziptest_{Guid.NewGuid():N}.zip");
        using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            build(zip);
        return path;
    }

    [Fact]
    public void TooManyEntries_Throws()
    {
        var path = CreateTempZip(zip =>
        {
            for (var i = 0; i < 21; i++) // limit is 20
            {
                var entry = zip.CreateEntry($"file{i}.txt");
                using var w = new StreamWriter(entry.Open());
                w.Write("x");
            }
        });

        try
        {
            Assert.Throws<InvalidDataException>(() => BibleFormatDispatcher.Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SuspiciousCompressionRatio_Throws()
    {
        // 1 MB of a single repeated byte deflates to a few KB → ratio well above 50:1.
        var path = CreateTempZip(zip =>
        {
            var entry = zip.CreateEntry("verses.txt", CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(new byte[1_000_000], 0, 1_000_000);
        });

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => BibleFormatDispatcher.Import(path));
            Assert.Contains("compression ratio", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
