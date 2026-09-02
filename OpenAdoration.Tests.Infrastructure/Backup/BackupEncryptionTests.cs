using System.Security.Cryptography;
using System.Text;
using OpenAdoration.Infrastructure.Backup;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Backup;

public sealed class BackupEncryptionTests
{
    [Fact]
    public void EncryptThenDecrypt_WithCorrectPassword_RoundTrips()
    {
        var plaintext = Encoding.UTF8.GetBytes("fake zip bytes for the library");

        var encrypted = BackupEncryption.Encrypt(plaintext, "correct horse battery staple");
        var decrypted = BackupEncryption.Decrypt(encrypted, "correct horse battery staple");

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_ThrowsCryptographicException()
    {
        var encrypted = BackupEncryption.Encrypt(Encoding.UTF8.GetBytes("secret library"), "right-password");

        // AesGcm throws AuthenticationTagMismatchException, a CryptographicException subtype —
        // ThrowsAny matches polymorphically the way ZipBackupService's catch clause does.
        Assert.ThrowsAny<CryptographicException>(() => BackupEncryption.Decrypt(encrypted, "wrong-password"));
    }

    [Fact]
    public void IsEncrypted_DistinguishesEncryptedFromPlainZip()
    {
        var encrypted = BackupEncryption.Encrypt(Encoding.UTF8.GetBytes("x"), "pw");
        var plainZip  = "PK\x03\x04-rest-of-a-zip-file"u8.ToArray();

        Assert.True(BackupEncryption.IsEncrypted(encrypted));
        Assert.False(BackupEncryption.IsEncrypted(plainZip));
    }
}
