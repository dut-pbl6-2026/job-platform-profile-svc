using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Profile.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core tooling (`dotnet ef migrations ...`).
/// Reads the connection string from the `DATABASE_URL_PROFILE` environment
/// variable — never hard-code connection strings in source (AGENTS.md).
/// The connection is never opened during scaffolding; any well-formed
/// Npgsql string is sufficient, e.g.:
/// `DATABASE_URL_PROFILE="Host=localhost;Database=job_platform_profile;Username=postgres" dotnet ef ...`
/// </summary>
public class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
{
    public ProfileDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_URL_PROFILE")
            ?? throw new InvalidOperationException(
                "Connection string not configured. Set DATABASE_URL_PROFILE.");
        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new ProfileDbContext(options);
    }
}
