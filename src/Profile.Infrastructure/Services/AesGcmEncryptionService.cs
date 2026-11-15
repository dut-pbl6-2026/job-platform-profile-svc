using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Profile.Core.Interfaces;

namespace Profile.Infrastructure.Services;

/// <summary>
/// Application-layer AES-256-GCM encryption for sensitive profile fields (SEC-08).
/// Storage format: Base64(Nonce[12] + Tag[16] + Ciphertext[N]). A fresh random nonce
/// is generated per encryption, so equal plaintexts never produce equal ciphertexts.
/// </summary>
public class AesGcmEncryptionService : IAesEncryptionService
{
    public const string KeyConfigurationName = "PROFILE_ENCRYPTION_KEY";

    private const int KeySize = 32;      // AES-256
    private const int NonceSize = 12;    // bytes
    private const int TagSize = 16;      // bytes

    private readonly byte[] _key;
    private readonly ILogger<AesGcmEncryptionService> _logger;

    public AesGcmEncryptionService(
        IConfiguration configuration,
        ILogger<AesGcmEncryptionService> logger)
    {
        _key = ParseKey(configuration[KeyConfigurationName]);
        _logger = logger;
    }

    /// <summary>
    /// Decodes the base64 master key and checks it is exactly 32 bytes.
    /// Throws at startup for a missing or malformed key in every environment.
    /// The key value is never included in the error message.
    /// </summary>
    public static byte[] ParseKey(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(
                $"{KeyConfigurationName} is required: a base64-encoded 256-bit (32-byte) key.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{KeyConfigurationName} must be valid base64.");
        }

        if (key.Length != KeySize)
            throw new InvalidOperationException(
                $"{KeyConfigurationName} must decode to exactly {KeySize} bytes, got {key.Length}.");

        return key;
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
        catch (Exception ex) when (ex is CryptographicException or FormatException or AuthenticationTagMismatchException)
        {
            _logger.LogError(ex, "AES-GCM decryption failed — data may be corrupted or key mismatch.");
            throw;
        }
    }
}
