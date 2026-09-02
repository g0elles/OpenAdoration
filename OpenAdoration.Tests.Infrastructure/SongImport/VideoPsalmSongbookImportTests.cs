using System.IO.Compression;
using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Helpers.SongImport;
using OpenAdoration.WPF.Helpers.SongImport.VideoPsalm;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.SongImport;

public sealed class VideoPsalmSongbookImportTests : IDisposable
{
    private readonly string _vpcPath = Path.Combine(Path.GetTempPath(), $"vp_{Guid.NewGuid():N}.vpc");

    // Same relaxed VideoPsalm dialect as .vpagd's Song_n.json, batched into one "Songs" array.
    private const string SongsJson =
        "{Description:\"unused\",Songs:[" +
        "{Guid:\"song-1\",Verses:[{\nText:\"First line\"},{ID:2,\nText:\"Chorus text\"}],\nText:\"Primera Canción\"}," +
        "{Guid:\"song-2\",Verses:[{\nText:\"Only verse\"}],\nText:\"Segunda Canción\"}" +
        "]}";

    private void BuildSongbook(string songsJson)
    {
        using var stream = File.Create(_vpcPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("Songs.json").Open());
        writer.Write(songsJson);
    }

    [Fact]
    public void ParseSongbook_ExtractsEverySong_WithSourceGuidForDedup()
    {
        BuildSongbook(SongsJson);

        var songs = VideoPsalmParser.ParseSongbook(_vpcPath);

        Assert.Equal(2, songs.Count);
        Assert.Equal("Primera Canción", songs[0].Title);
        Assert.Equal("song-1", songs[0].SourceGuid);
        Assert.Equal(2, songs[0].Sections.Count);
        Assert.All(songs[0].Sections, s => Assert.Equal(SectionType.Verse, s.Type));
        Assert.Equal("song-2", songs[1].SourceGuid);
    }

    [Fact]
    public void ParseSongbook_NoSongsJsonEntry_Throws()
    {
        using (var stream = File.Create(_vpcPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("Other.json").Open()))
            writer.Write("{}");

        Assert.Throws<InvalidDataException>(() => VideoPsalmParser.ParseSongbook(_vpcPath));
    }

    [Fact]
    public void ParseSongbook_DrmProtectedFile_ThrowsIsBibleException()
    {
        // Minimal fake ZIP local-file-header: signature "PK\x03\x04" + AES compression method (99) at offset 8.
        byte[] header = [(byte)'P', (byte)'K', 3, 4, 0, 0, 0, 0, 99, 0];
        File.WriteAllBytes(_vpcPath, header);

        Assert.Throws<VideoPsalmSongbookIsBibleException>(() => VideoPsalmParser.ParseSongbook(_vpcPath));
    }

    [Fact]
    public void Dispatcher_ImportMany_RoutesVpcToSongbook()
    {
        BuildSongbook(SongsJson);

        var songs = SongFormatDispatcher.ImportMany(_vpcPath);

        Assert.Equal(2, songs.Count);
    }

    public void Dispose()
    {
        if (File.Exists(_vpcPath)) File.Delete(_vpcPath);
    }
}
