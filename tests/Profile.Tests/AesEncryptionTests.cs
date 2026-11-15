using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;
using Profile.Infrastructure.Services;

namespace Profile.Tests;

/// <summary>
/// Unit tests for AES-256-GCM application-layer encryption (SEC-08):
/// round-trip, nonce randomness, tamper detection, wrong key, and key validation.
/// </summary>
public class AesEncryptionTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmEncryptionService CreateService(string key) =>
        new(new DictionaryConfiguration(new Dictionary<string, string?>
        {
            [AesGcmEncryptionService.KeyConfigurationName] = key,
        }),
            NullLogger<AesGcmEncryptionService>.Instance);

    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginal()
    {
        var svc = CreateService(NewKey());

        const string plain = "0901234567 — 123 Lê Duẩn, Đà Nẵng";
        var cipher = svc.Encrypt(plain);

        Assert.NotNull(cipher);
        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, svc.Decrypt(cipher));
    }

    [Fact]
    public void EncryptDecrypt_Null_ReturnsNull()
    {
        var svc = CreateService(NewKey());

        Assert.Null(svc.Encrypt(null));
        Assert.Null(svc.Decrypt(null));
    }

    [Fact]
    public void Encrypt_TwiceSamePlaintext_ProducesDifferentCiphertexts()
    {
        var svc = CreateService(NewKey());

        var c1 = svc.Encrypt("same plaintext");
        var c2 = svc.Encrypt("same plaintext");

        Assert.NotEqual(c1, c2);
        Assert.Equal("same plaintext", svc.Decrypt(c1));
        Assert.Equal("same plaintext", svc.Decrypt(c2));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsAuthenticationTagMismatch()
    {
        var svc = CreateService(NewKey());
        var cipher = svc.Encrypt("sensitive")!;

        // Flip a middle character to a different valid Base64 character (never padding).
        const string b64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var i = cipher.Length / 2;
        var replacement = b64.First(c => c != cipher[i]);
        var tampered = cipher[..i] + replacement + cipher[(i + 1)..];

        Assert.Throws<AuthenticationTagMismatchException>(() => svc.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_InvalidBase64_ThrowsFormatException()
    {
        var svc = CreateService(NewKey());

        Assert.Throws<FormatException>(() => svc.Decrypt("!!! not base64 at all !!!"));
    }

    [Fact]
    public void Decrypt_TooShortPayload_ThrowsCryptographicException()
    {
        var svc = CreateService(NewKey());
        var tooShort = Convert.ToBase64String(new byte[5]); // shorter than nonce(12) + tag(16)

        Assert.Throws<CryptographicException>(() => svc.Decrypt(tooShort));
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsAuthenticationTagMismatch()
    {
        var cipher = CreateService(NewKey()).Encrypt("sensitive")!;

        Assert.Throws<AuthenticationTagMismatchException>(() => CreateService(NewKey()).Decrypt(cipher));
    }

    [Fact]
    public void ParseKey_ValidBase64Key_ReturnsRawKeyBytes()
    {
        var raw = RandomNumberGenerator.GetBytes(32);

        Assert.Equal(raw, AesGcmEncryptionService.ParseKey(Convert.ToBase64String(raw)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseKey_Missing_Throws(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AesGcmEncryptionService.ParseKey(key));
        Assert.Contains("PROFILE_ENCRYPTION_KEY is required", ex.Message);
    }

    [Fact]
    public void ParseKey_NotBase64_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => AesGcmEncryptionService.ParseKey("not base64 !!!"));
        Assert.Contains("must be valid base64", ex.Message);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void ParseKey_WrongLength_Throws(int byteCount)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));

        var ex = Assert.Throws<InvalidOperationException>(() => AesGcmEncryptionService.ParseKey(key));
        Assert.Contains($"exactly 32 bytes, got {byteCount}", ex.Message);
    }

    [Fact]
    public void ParseKey_ErrorMessage_DoesNotEchoKeyValue()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(10));

        var ex = Assert.Throws<InvalidOperationException>(() => AesGcmEncryptionService.ParseKey(key));
        Assert.DoesNotContain(key, ex.Message);
    }

    private sealed class DictionaryConfiguration(Dictionary<string, string?> data) : IConfiguration
    {
        public string? this[string key]
        {
            get => data.TryGetValue(key, out var value) ? value : null;
            set => data[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new CancellationChangeToken(new CancellationTokenSource().Token);
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }
}
