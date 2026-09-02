using System.Security.Cryptography;
using System.Text;

namespace OpenAdoration.Infrastructure.Backup;

/// <summary>
/// Optional password-based encryption for a packed <c>.oabak</c>. A plain backup is a readable
/// zip of the whole library (DB + media + settings), so anyone with file access to it — cloud
/// sync, a shared drive, a stolen laptop — can open it. AES-256-GCM (confidentiality + tamper
/// detection in one primitive) with a PBKDF2-HMACSHA256 key (600k iterations — OWASP 2023's
/// floor for PBKDF2-SHA256) derived from the backup password the operator chooses.
/// File layout: magic(8) + salt(16) + nonce(12) + tag(16) + ciphertext. An unencrypted backup is
/// still a plain zip (starts with "PK"), so old backups and the no-password choice both keep
/// working unchanged.
/// </summary>
public static class BackupEncryption
{
    private static readonly byte[] Magic = "OABAKAES"u8.ToArray();

    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Pbkdf2Iterations = 600_000;
    private const int KeySize = 32; // AES-256

    public static bool IsEncrypted(byte[] fileBytes) =>
        fileBytes.Length >= Magic.Length && fileBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    public static byte[] Encrypt(byte[] plaintext, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt  = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key   = DeriveKey(password, salt);

        var ciphertext = new byte[plaintext.Length];
        var tag        = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var output = new byte[Magic.Length + SaltSize + NonceSize + TagSize + ciphertext.Length];
        var pos = 0;
        Magic.CopyTo(output.AsSpan(pos));      pos += Magic.Length;
        salt.CopyTo(output.AsSpan(pos));       pos += SaltSize;
        nonce.CopyTo(output.AsSpan(pos));      pos += NonceSize;
        tag.CopyTo(output.AsSpan(pos));        pos += TagSize;
        ciphertext.CopyTo(output.AsSpan(pos));
        return output;
    }

    /// <summary>Throws <see cref="CryptographicException"/> on a wrong password or a tampered/corrupt file (auth tag mismatch).</summary>
    public static byte[] Decrypt(byte[] fileBytes, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (!IsEncrypted(fileBytes))
            throw new InvalidDataException("Not an encrypted backup.");

        var pos = Magic.Length;
        var salt  = fileBytes.AsSpan(pos, SaltSize).ToArray();  pos += SaltSize;
        var nonce = fileBytes.AsSpan(pos, NonceSize).ToArray(); pos += NonceSize;
        var tag   = fileBytes.AsSpan(pos, TagSize).ToArray();   pos += TagSize;
        var ciphertext = fileBytes.AsSpan(pos).ToArray();

        var key = DeriveKey(password, salt);
        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, TagSize))
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
}
