using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Profile.Tests;

/// <summary>
/// Startup must fail for a missing or malformed PROFILE_ENCRYPTION_KEY in every environment.
/// The host fails before it touches the database, so no Postgres is needed here.
/// </summary>
public class StartupConfigurationTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task Startup_MissingEncryptionKey_Fails(string environment)
    {
        var ex = await StartupExceptionAsync(environment, encryptionKey: null);

        Assert.NotNull(ex);
        Assert.Contains("PROFILE_ENCRYPTION_KEY is required", ex.Message);
    }

    [Theory]
    [InlineData("Development", 16)]
    [InlineData("Production", 16)]
    [InlineData("Development", 31)]
    [InlineData("Production", 33)]
    public async Task Startup_WrongLengthEncryptionKey_Fails(string environment, int byteCount)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));

        var ex = await StartupExceptionAsync(environment, key);

        Assert.NotNull(ex);
        Assert.Contains($"exactly 32 bytes, got {byteCount}", ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Startup_NonBase64EncryptionKey_Fails(string environment)
    {
        var ex = await StartupExceptionAsync(environment, "dev-profile-enc-key-change-me!!");

        Assert.NotNull(ex);
        Assert.Contains("must be valid base64", ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Startup_MissingDatabaseConnectionString_Fails(string environment)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Both sources blank: no committed fallback may be used in any environment.
        var ex = await StartupExceptionAsync(environment, key, databaseUrl: "", connectionStringsProfileDb: "");

        Assert.NotNull(ex);
        Assert.Contains("Connection string not configured", ex.Message);
    }

    [Fact]
    public async Task Startup_DevAuthEnabled_OutsideDevelopment_Fails()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var ex = await StartupExceptionAsync("Production", key, devAuthEnabled: "true");

        Assert.NotNull(ex);
        Assert.Contains("DEV_AUTH_ENABLED is only allowed", ex.Message);
    }

    /// <summary>
    /// Builds the real app with the given encryption key and returns the startup exception, or null.
    /// Settings are passed with UseSetting so they override anything inherited from the process environment.
    /// </summary>
    private static async Task<Exception?> StartupExceptionAsync(
        string environment, string? encryptionKey, string? devAuthEnabled = null,
        string? databaseUrl = "Host=127.0.0.1;Port=1;Database=unused;Username=x;Password=x",
        string? connectionStringsProfileDb = null)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("PROFILE_ENCRYPTION_KEY", encryptionKey ?? string.Empty);
            builder.UseSetting("DEV_AUTH_ENABLED", devAuthEnabled ?? "false");
            builder.UseSetting("JWT_SECRET", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("DATABASE_URL_PROFILE", databaseUrl ?? string.Empty);
            builder.UseSetting("ConnectionStrings:ProfileDb", connectionStringsProfileDb ?? string.Empty);
        });

        try
        {
            using var client = factory.CreateClient();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
