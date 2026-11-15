using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Profile.Infrastructure.Services;
using Xunit;

namespace Profile.Tests;

/// <summary>
/// Versioned ciphertext (SEC-08 key rotation): each record carries a key id byte, the active key
/// encrypts new data, and retired keys decrypt older data only.
/// </summary>
public class AesVersionedCiphertextTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmEncryptionService Create(params (string Key, string? Value)[] values)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();
        return new AesGcmEncryptionService(cfg, NullLogger<AesGcmEncryptionService>.Instance);
    }

    private static AesGcmEncryptionService Active(string key, string? keyId = null, string? retired = null) =>
        Create(
            (AesGcmEncryptionService.KeyConfigurationName, key),
            (AesGcmEncryptionService.KeyIdConfigurationName, keyId),
            (AesGcmEncryptionService.RetiredKeysConfigurationName, retired));

    [Fact]
    public void Ciphertext_StartsWithActiveKeyId_DefaultingToOne()
    {
        var cipher = Active(NewKey()).Encrypt("0901234567")!;

        Assert.Equal(1, Convert.FromBase64String(cipher)[0]);
    }

    [Fact]
    public void RoundTrip_WithTwoKeys_OldRecordStillDecryptsAfterRotation()
    {
        var keyA = NewKey();
        var keyB = NewKey();

        // Before rotation: key A is active (id 1).
        var before = Active(keyA);
        var oldRecord = before.Encrypt("123 Le Duan, Da Nang")!;

        // Rotation: key B becomes active (id 2) and key A is kept as a retired, decrypt-only key.
        var after = Active(keyB, "2", $"1:{keyA}");

        Assert.Equal("123 Le Duan, Da Nang", after.Decrypt(oldRecord));

        var newRecord = after.Encrypt("0909999999")!;
        Assert.Equal(2, Convert.FromBase64String(newRecord)[0]);
        Assert.Equal("0909999999", after.Decrypt(newRecord));
        Assert.Equal("0909999999", Active(keyB, "2", $"1:{keyA}").Decrypt(newRecord));
    }

    [Fact]
    public void ReplicaWithRetiredKey_DecryptsOldRecordAndWritesActiveId()
    {
        var keyA = NewKey();
        var keyB = NewKey();
        var record = Active(keyA).Encrypt("secret")!;

        // A replica that has not yet been given the new active key must still read the record
        // when the retired key is present, and must write with its own active key.
        var replica = Active(keyA, "1", $"2:{keyB}");
        Assert.Equal("secret", replica.Decrypt(record));
        Assert.Equal(1, Convert.FromBase64String(replica.Encrypt("x")!)[0]);
    }

    [Fact]
    public void Decrypt_UnknownKeyId_ThrowsCryptographicException()
    {
        var record = Active(NewKey(), "1").Encrypt("secret")!;

        var ex = Assert.Throws<CryptographicException>(() => Active(NewKey(), "3").Decrypt(record));
        Assert.Contains("unknown key id 1", ex.Message);
    }

    [Fact]
    public void Decrypt_RetiredKeyWithWrongMaterial_ThrowsAuthenticationTagMismatch()
    {
        var keyA = NewKey();
        var record = Active(keyA).Encrypt("secret")!;

        // Same id, different key material: the tag check must fail, not return garbage.
        var wrong = Active(NewKey(), "2", $"1:{NewKey()}");
        Assert.Throws<AuthenticationTagMismatchException>(() => wrong.Decrypt(record));
    }

    [Fact]
    public void Decrypt_TamperedKeyIdByte_Fails()
    {
        var keyA = NewKey();
        var keyB = NewKey();
        var record = Active(keyA).Encrypt("secret")!;
        var bytes = Convert.FromBase64String(record);
        bytes[0] = 2; // claim key id 2, which is keyB here

        var svc = Active(keyA, "1", $"2:{keyB}");
        Assert.ThrowsAny<CryptographicException>(() => svc.Decrypt(Convert.ToBase64String(bytes)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("256")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void KeyId_OutOfRange_Throws(string keyId)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Active(NewKey(), keyId));
        Assert.Contains("PROFILE_ENCRYPTION_KEY_ID must be an integer from 1 to 255", ex.Message);
    }

    [Fact]
    public void Retired_DuplicateId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Active(NewKey(), "2", $"1:{NewKey()},1:{NewKey()}"));
        Assert.Contains("repeats key id 1", ex.Message);
    }

    [Fact]
    public void Retired_IdEqualToActive_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Active(NewKey(), "2", $"2:{NewKey()}"));
        Assert.Contains("repeats key id 2", ex.Message);
    }

    [Fact]
    public void Retired_WrongLength_ThrowsWithoutEchoingKey()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var ex = Assert.Throws<InvalidOperationException>(() => Active(NewKey(), "2", $"1:{shortKey}"));
        Assert.Contains("PROFILE_ENCRYPTION_KEYS_RETIRED key id 1 must decode to exactly 32 bytes, got 16", ex.Message);
        Assert.DoesNotContain(shortKey, ex.Message);
    }

    [Theory]
    [InlineData("no-colon-here")]
    [InlineData(":")]
    [InlineData("x:")]
    public void Retired_MalformedEntry_Throws(string entry)
    {
        Assert.ThrowsAny<InvalidOperationException>(() => Active(NewKey(), "2", entry));
    }

    [Fact]
    public void Retired_InvalidBase64_ThrowsWithoutEchoingValue()
    {
        const string bad = "not base64 !!!";

        var ex = Assert.Throws<InvalidOperationException>(() => Active(NewKey(), "2", $"1:{bad}"));
        Assert.Contains("must be valid base64", ex.Message);
        Assert.DoesNotContain(bad, ex.Message);
    }

    [Fact]
    public void ActiveKey_StillRequired_EvenWhenRetiredKeysPresent()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Create((AesGcmEncryptionService.RetiredKeysConfigurationName, $"1:{NewKey()}")));
        Assert.Contains("PROFILE_ENCRYPTION_KEY is required", ex.Message);
    }
}
