using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Profile.Core.Interfaces;

namespace Profile.Infrastructure.Services;

/// <summary>
/// Triển khai mã hóa AES-256-GCM tầng ứng dụng — SEC-08.
/// Format lưu trữ: Base64(Nonce[12] + Tag[16] + Ciphertext[N]).
/// Mỗi lần Encrypt sinh Nonce ngẫu nhiên 12 bytes mới — đảm bảo hai ciphertext
/// của cùng plaintext luôn khác nhau (probabilistic encryption).
/// </summary>
public class AesGcmEncryptionService : IAesEncryptionService
{
    // AES-256-GCM constants
    private const int NonceSize = 12;    // bytes
    private const int TagSize = 16;      // bytes

    private readonly byte[] _key;
    private readonly ILogger<AesGcmEncryptionService> _logger;

    public AesGcmEncryptionService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AesGcmEncryptionService> logger)
    {
        _logger = logger;
        var keyStr = configuration["PROFILE_ENCRYPTION_KEY"]
                     ?? configuration["ENCRYPTION_KEY"];

        if (string.IsNullOrWhiteSpace(keyStr))
        {
            if (!environment.IsDevelopment())
            {
                // SEC-08 fail-fast: never boot non-Development on a public dev key.
                throw new InvalidOperationException(
                    "PROFILE_ENCRYPTION_KEY (or ENCRYPTION_KEY) is not configured. " +
                    "Set it before deploying outside Development.");
            }

            // SEC-08: Fallback dev key — Development ONLY, never Production.
            keyStr = "dev-profile-enc-key-change-me!!";
            _logger.LogWarning(
                "PROFILE_ENCRYPTION_KEY not set — using insecure dev key. " +
                "Set PROFILE_ENCRYPTION_KEY (32+ chars) before deploying.");
        }

        // Derive exactly 32 bytes for AES-256 using SHA-256 of the key string
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyStr));
    }

    /// <inheritdoc/>
    public string? Encrypt(string? plainText)
    {
        if (plainText is null) return null;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Pack: Nonce[12] + Tag[16] + Ciphertext[N]
        var packed = new byte[NonceSize + TagSize + cipherBytes.Length];
        nonce.CopyTo(packed, 0);
        tag.CopyTo(packed, NonceSize);
        cipherBytes.CopyTo(packed, NonceSize + TagSize);

        return Convert.ToBase64String(packed);
    }

    /// <inheritdoc/>
    public string? Decrypt(string? cipherText)
    {
        if (cipherText is null) return null;

        try
        {
            var packed = Convert.FromBase64String(cipherText);

            if (packed.Length < NonceSize + TagSize)
                throw new CryptographicException("Ciphertext too short — corrupted data.");

            var nonce = packed[..NonceSize];
            var tag = packed[NonceSize..(NonceSize + TagSize)];
            var cipherBytes = packed[(NonceSize + TagSize)..];
            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogError(ex, "AES-GCM decryption failed — data may be corrupted or key mismatch.");
            throw;
        }
    }
}
