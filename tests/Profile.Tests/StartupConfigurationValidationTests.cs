using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Profile.Api.Auth;
using Profile.Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Profile.Tests;

/// <summary>
/// Unit tests for startup configuration rules: the connection string comes only from configuration,
/// a blank value counts as missing, and DEV_AUTH_ENABLED is refused outside Development.
/// </summary>
public class StartupConfigurationValidationTests
{
    private const string Conn = "Host=db;Port=5432;Database=job_platform_profile;Username=u;Password=p";

    private static IConfiguration Config(params (string Key, string? Value)[] values)
    {
        var data = values.ToDictionary(v => v.Key, v => v.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Profile.Api";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void ConnectionString_FromConnectionStrings_IsUsed()
    {
        var cfg = Config(("ConnectionStrings:ProfileDb", Conn));

        Assert.Equal(Conn, StartupConfiguration.GetDatabaseConnectionString(cfg));
    }

    [Fact]
    public void ConnectionString_FromDatabaseUrlVariable_IsUsedWhenConnectionStringsAbsent()
    {
        var cfg = Config((StartupConfiguration.DatabaseUrlVariable, Conn));

        Assert.Equal(Conn, StartupConfiguration.GetDatabaseConnectionString(cfg));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "\t")]
    [InlineData("", null)]
    public void ConnectionString_MissingOrBlank_Throws(string? connectionStrings, string? databaseUrl)
    {
        var cfg = Config(
            ("ConnectionStrings:ProfileDb", connectionStrings),
            (StartupConfiguration.DatabaseUrlVariable, databaseUrl));

        var ex = Assert.Throws<InvalidOperationException>(() => StartupConfiguration.GetDatabaseConnectionString(cfg));
        Assert.Contains(StartupConfiguration.DatabaseUrlVariable, ex.Message);
    }

    [Fact]
    public void ConnectionString_BlankConnectionStringsFallsBackToVariable()
    {
        var cfg = Config(
            ("ConnectionStrings:ProfileDb", ""),
            (StartupConfiguration.DatabaseUrlVariable, Conn));

        Assert.Equal(Conn, StartupConfiguration.GetDatabaseConnectionString(cfg));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void JwtMetadataAddress_MissingOrBlank_FailsStartup(string? address)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddRs256JwtBearer(Config(("Jwt:MetadataAddress", address))));

        Assert.Contains("Jwt:MetadataAddress", ex.Message);
    }

    [Fact]
    public void DevAuth_Enabled_InProduction_Throws()
    {
        var cfg = Config((StartupConfiguration.DevAuthVariable, "true"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StartupConfiguration.ValidateDevAuth(cfg, new FakeEnvironment("Production")));
        Assert.Contains("only allowed in the Development", ex.Message);
    }

    [Fact]
    public void DevAuth_Enabled_InDevelopment_IsAccepted()
    {
        var cfg = Config((StartupConfiguration.DevAuthVariable, "true"));

        StartupConfiguration.ValidateDevAuth(cfg, new FakeEnvironment("Development"));
    }

    [Fact]
    public void DevAuth_Disabled_InProduction_IsAccepted()
    {
        StartupConfiguration.ValidateDevAuth(Config(), new FakeEnvironment("Production"));
    }
}
