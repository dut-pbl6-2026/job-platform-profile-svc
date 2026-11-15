using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Profile.Core.Interfaces;

namespace Profile.Infrastructure.Services;

/// <summary>
/// Application-layer AES-256-GCM encryption for sensitive profile fields (SEC-08).
/// Storage format (versioned): Base64(KeyId[1] + Nonce[12] + Tag[16] + Ciphertext[N]).
/// The key id selects the key used to decrypt the record, so the active key can change while
/// older records stay readable: keys listed in PROFILE_ENCRYPTION_KEYS_RETIRED are used for
/// decryption only. A fresh random nonce is generated per encryption.
/// </summary>
public class AesGcmEncryptionService : IAesEncryptionService
{
    /// <summary>Active key (base64 of 32 random bytes). Used for all new encryptions.</summary>
    public const string KeyConfigurationName = "PROFILE_ENCRYPTION_KEY";

    /// <summary>Key id stamped on new ciphertext (1-255, default 1).</summary>
    public const string KeyIdConfigurationName = "PROFILE_ENCRYPTION_KEY_ID";

    /// <summary>Decrypt-only keys: comma-separated "id:base64key" entries (ids 1-255, unique, not the active id).
    /// Holds any key that is not active: one announced before a switch, or one retired after it.</summary>
    public const string RetiredKeysConfigurationName = "PROFILE_ENCRYPTION_KEYS_RETIRED";

    private const int KeySize = 32;      // AES-256
    private const int NonceSize = 12;    // bytes
    private const int TagSize = 16;      // bytes
    private const int KeyIdSize = 1;     // bytes
    private const int HeaderSize = KeyIdSize + NonceSize + TagSize;
    private const byte DefaultActiveKeyId = 1;

    private readonly byte _activeKeyId;
    private readonly byte[] _activeKey;
    private readonly IReadOnlyDictionary<byte, byte[]> _keysById;
    private readonly ILogger<AesGcmEncryptionService> _logger;

    public AesGcmEncryptionService(
        IConfiguration configuration,
        ILogger<AesGcmEncryptionService> logger)
    {
        _activeKey = ParseKey(configuration[KeyConfigurationName], KeyConfigurationName);
        _activeKeyId = ParseKeyId(configuration[KeyIdConfigurationName]);

        var keysById = new Dictionary<byte, byte[]> { [_activeKeyId] = _activeKey };
        foreach (var (id, key) in ParseRetiredKeys(configuration[RetiredKeysConfigurationName]))
        {
            if (!keysById.TryAdd(id, key))
                throw new InvalidOperationException(
                    $"{RetiredKeysConfigurationName} repeats key id {id}, or it equals the active key id.");
        }

        _keysById = keysById;
        _logger = logger;
    }

    /// <summary>
    /// Decodes the base64 master key and checks it is exactly 32 bytes.
    /// Throws at startup for a missing or malformed key in every environment.
    /// The key value is never included in the error message.
    /// </summary>
    public static byte[] ParseKey(string? base64Key) => ParseKey(base64Key, KeyConfigurationName);

    private static byte[] ParseKey(string? base64Key, string setting)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(
                $"{setting} is required: a base64-encoded 256-bit (32-byte) key.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{setting} must be valid base64.");
        }

        if (key.Length != KeySize)
            throw new InvalidOperationException(
                $"{setting} must decode to exactly {KeySize} bytes, got {key.Length}.");

        return key;
    }

    private static byte ParseKeyId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultActiveKeyId;

        if (!byte.TryParse(raw.Trim(), out var id) || id == 0)
            throw new InvalidOperationException($"{KeyIdConfigurationName} must be an integer from 1 to 255.");

        return id;
    }

    private static IEnumerable<(byte Id, byte[] Key)> ParseRetiredKeys(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf(':');
            if (separator <= 0)
                throw new InvalidOperationException(
                    $"{RetiredKeysConfigurationName} entries must have the form id:base64key.");

            var idText = entry[..separator].Trim();
            if (!byte.TryParse(idText, out var id) || id == 0)
                throw new InvalidOperationException(
                    $"{RetiredKeysConfigurationName} key ids must be integers from 1 to 255.");

            // The message names the id, never the key value.
            yield return (id, ParseKey(entry[(separator + 1)..].Trim(), $"{RetiredKeysConfigurationName} key id {id}"));
        }
    }

    /// <inheritdoc/>
    public string? Encrypt(string? plainText)
    {
        if (plainText is null) return null;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_activeKey, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var packed = new byte[HeaderSize + cipherBytes.Length];
        packed[0] = _activeKeyId;
        nonce.CopyTo(packed, KeyIdSize);
        tag.CopyTo(packed, KeyIdSize + NonceSize);
        cipherBytes.CopyTo(packed, HeaderSize);

        return Convert.ToBase64String(packed);
    }

    /// <inheritdoc/>
    public string? Decrypt(string? cipherText)
    {
        if (cipherText is null) return null;

        try
        {
            var packed = Convert.FromBase64String(cipherText);

            if (packed.Length < HeaderSize)
                throw new CryptographicException("Ciphertext too short — corrupted data.");

            var keyId = packed[0];
            if (!_keysById.TryGetValue(keyId, out var key))
                throw new CryptographicException($"Ciphertext was encrypted with unknown key id {keyId}.");

            var nonce = packed[KeyIdSize..(KeyIdSize + NonceSize)];
            var tag = packed[(KeyIdSize + NonceSize)..HeaderSize];
            var cipherBytes = packed[HeaderSize..];
            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(key, TagSize);
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
