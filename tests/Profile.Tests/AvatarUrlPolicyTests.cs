using Profile.Core.Validation;
using Xunit;

namespace Profile.Tests;

/// <summary>Unit tests for the avatar URL boundary rules: absolute https only, max 512 characters.</summary>
public class AvatarUrlPolicyTests
{
    [Theory]
    [InlineData("https://cdn.example.com/a.png", "https://cdn.example.com/a.png")]
    [InlineData("  https://cdn.example.com/a.png  ", "https://cdn.example.com/a.png")]
    [InlineData("HTTPS://cdn.example.com/a.png", "HTTPS://cdn.example.com/a.png")]
    [InlineData("https://cdn.example.com:8443/path?x=1", "https://cdn.example.com:8443/path?x=1")]
    public void Accepts_AbsoluteHttpsUrl(string input, string expected)
    {
        Assert.True(AvatarUrlPolicy.TryNormalize(input, out var normalized, out var error));
        Assert.Equal(expected.Trim(), normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_ClearsAvatar(string? input)
    {
        Assert.True(AvatarUrlPolicy.TryNormalize(input, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://cdn.example.com/a.png")]
    [InlineData("ftp://cdn.example.com/a.png")]
    [InlineData("//cdn.example.com/a.png")]
    [InlineData("/a.png")]
    [InlineData("cdn.example.com/a.png")]
    [InlineData("https://")]
    [InlineData("https:///a.png")]
    [InlineData("https://user:pw@cdn.example.com/a.png")]
    [InlineData("https://cdn.example.com/a b.png")]
    [InlineData("https://cdn.example.com/a\tb.png")]
    public void Rejects_NonHttpsOrMalformed(string input)
    {
        Assert.False(AvatarUrlPolicy.TryNormalize(input, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.NotNull(error);
    }

    [Fact]
    public void Accepts_ExactlyMaxLength()
    {
        var url = "https://example.com/" + new string('a', AvatarUrlPolicy.MaxLength - "https://example.com/".Length);
        Assert.Equal(AvatarUrlPolicy.MaxLength, url.Length);

        Assert.True(AvatarUrlPolicy.TryNormalize(url, out _, out _));
    }

    [Fact]
    public void Rejects_OneOverMaxLength()
    {
        var url = "https://example.com/" + new string('a', AvatarUrlPolicy.MaxLength - "https://example.com/".Length + 1);

        Assert.False(AvatarUrlPolicy.TryNormalize(url, out _, out var error));
        Assert.Contains("512", error);
    }
}
