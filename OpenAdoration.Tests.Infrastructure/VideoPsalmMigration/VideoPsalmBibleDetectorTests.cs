using OpenAdoration.WPF.Helpers.VideoPsalmMigration;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.VideoPsalmMigration;

public sealed class VideoPsalmBibleDetectorTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"vpc_{Guid.NewGuid():N}.vpc");

    [Fact]
    public void IsDrmProtected_AesEntry_True()
    {
        // Minimal ZIP local file header with compression method 99 (WinZip AES) at offset 8.
        File.WriteAllBytes(_path, [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x01, 0x00, 0x63, 0x00, 0x00, 0x00]);

        Assert.True(VideoPsalmBibleDetector.IsDrmProtected(_path));
    }

    [Fact]
    public void IsDrmProtected_PlainDeflateEntry_False()
    {
        // Same header but method 8 (deflate) — not AES.
        File.WriteAllBytes(_path, [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00]);

        Assert.False(VideoPsalmBibleDetector.IsDrmProtected(_path));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
