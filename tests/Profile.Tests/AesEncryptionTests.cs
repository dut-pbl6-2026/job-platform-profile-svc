using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;
using Profile.Infrastructure.Services;

namespace Profile.Tests;

/// <summary>
/// Unit tests cho AES-256-GCM application-layer encryption (SEC-08).
/// Roundtrip, nonce randomness, tamper detection, wrong-key, fail-fast.
/// </summary>
public class AesEncryptionTests
{
    private const string TestKey = "test-profile-encryption-key-32-chars!!";
    private const string OtherKey = "other-profile-encryption-key-32chars!";

    private static AesGcmEncryptionService CreateService(string? key, string environment = "Development")
    {
        var data = new Dictionary<string, string?>();
        if (key is not null)
            data["PROFILE_ENCRYPTION_KEY"] = key;
        return new AesGcmEncryptionService(
            new DictionaryConfiguration(data),
            new TestHostEnvironment(environment),
            NullLogger<AesGcmEncryptionService>.Instance);
    }

    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginal()
    {
        var svc = CreateService(TestKey);

        const string plain = "0901234567 — 123 Lê Duẩn, Đà Nẵng";
        var cipher = svc.Encrypt(plain);

        Assert.NotNull(cipher);
        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, svc.Decrypt(cipher));
    }

    [Fact]
    public void EncryptDecrypt_Null_ReturnsNull()
    {
        var svc = CreateService(TestKey);

        Assert.Null(svc.Encrypt(null));
        Assert.Null(svc.Decrypt(null));
    }

    [Fact]
    public void Encrypt_TwiceSamePlaintext_ProducesDifferentCiphertexts()
    {
        var svc = CreateService(TestKey);

        var c1 = svc.Encrypt("same plaintext");
        var c2 = svc.Encrypt("same plaintext");

        // Per-record random 12-byte nonce ⇒ probabilistic encryption.
        Assert.NotEqual(c1, c2);
        Assert.Equal("same plaintext", svc.Decrypt(c1));
        Assert.Equal("same plaintext", svc.Decrypt(c2));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var svc = CreateService(TestKey);
        var cipher = svc.Encrypt("sensitive")!;

        // Flip a middle char to another valid Base64 char ⇒ tag mismatch.
        // (Middle of the payload — never padding — so decoding still succeeds.)
        const string b64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var i = cipher.Length / 2;
        var replacement = b64.First(c => c != cipher[i]);
        var tampered = cipher[..i] + replacement + cipher[(i + 1)..];

        // .NET 10 throws AuthenticationTagMismatchException on tag mismatch.
        Assert.Throws<AuthenticationTagMismatchException>(() => svc.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_InvalidBase64_ThrowsFormatException()
    {
        var svc = CreateService(TestKey);

        Assert.Throws<FormatException>(() => svc.Decrypt("!!! not base64 at all !!!"));
    }

    [Fact]
    public void Decrypt_TooShortPayload_ThrowsCryptographicException()
    {
        var svc = CreateService(TestKey);
        var tooShort = Convert.ToBase64String(new byte[5]); // < nonce(12) + tag(16)

        Assert.Throws<CryptographicException>(() => svc.Decrypt(tooShort));
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsAuthenticationTagMismatch()
    {
        var cipher = CreateService(TestKey).Encrypt("sensitive")!;

        Assert.Throws<AuthenticationTagMismatchException>(() => CreateService(OtherKey).Decrypt(cipher));
    }

    [Fact]
    public void Ctor_MissingKey_NonDevelopment_ThrowsFailFast()
    {
        Assert.Throws<InvalidOperationException>(() => CreateService(null, "Production"));
        Assert.Throws<InvalidOperationException>(() => CreateService(null, "Staging"));
        Assert.Throws<InvalidOperationException>(() => CreateService("  ", "Production"));
    }

    [Fact]
    public void Ctor_MissingKey_Development_FallsBackToDevKey()
    {
        var svc = CreateService(null, "Development");

        Assert.Equal("hello", svc.Decrypt(svc.Encrypt("hello")));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Profile.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>
    /// Minimal dictionary-backed <see cref="IConfiguration"/> — the service under
    /// test only reads the string indexer, so no configuration provider needed.
    /// </summary>
    private sealed class DictionaryConfiguration(Dictionary<string, string?> data) : IConfiguration
    {
        public string? this[string key]
        {
            get => data.TryGetValue(key, out var value) ? value : null;
            set => data[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new CancellationChangeToken(new CancellationTokenSource().Token);
        public IConfigurationSection GetSection(string key) => new DictionarySection(key, null);
    }

    private sealed class DictionarySection(string key, string? value) : IConfigurationSection
    {
        public string? this[string k] { get => null; set { } }
        public string Key => key;
        public string Path => key;
        public string? Value { get; set; } = value;
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new CancellationChangeToken(new CancellationTokenSource().Token);
        public IConfigurationSection GetSection(string k) => new DictionarySection(k, null);
    }
}
