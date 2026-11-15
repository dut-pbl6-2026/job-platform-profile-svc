using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Profile.Api.Configuration;

/// <summary>
/// Reads the settings the service cannot start without. Nothing is hard-coded: the database
/// connection string comes only from configuration (ConnectionStrings:ProfileDb, or the
/// DATABASE_URL_PROFILE environment variable), in every environment. Missing or blank values
/// stop startup.
/// </summary>
public static class StartupConfiguration
{
    public const string DatabaseConnectionName = "ProfileDb";
    public const string DatabaseUrlVariable = "DATABASE_URL_PROFILE";
    public const string JwtSecretVariable = "JWT_SECRET";
    public const string DevAuthVariable = "DEV_AUTH_ENABLED";

    private const int MinJwtSecretBytes = 32;

    public static string GetDatabaseConnectionString(IConfiguration configuration)
    {
        var fromConnectionStrings = configuration.GetConnectionString(DatabaseConnectionName);
        if (!string.IsNullOrWhiteSpace(fromConnectionStrings))
            return fromConnectionStrings;

        var fromEnvironment = configuration[DatabaseUrlVariable];
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        throw new InvalidOperationException(
            $"Connection string not configured. Set {DatabaseUrlVariable} (or ConnectionStrings__{DatabaseConnectionName}).");
    }

    /// <summary>Returns the HS256 signing secret. It must be at least 32 bytes (UTF-8).</summary>
    public static string GetJwtSecret(IConfiguration configuration)
    {
        var secret = configuration[JwtSecretVariable];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < MinJwtSecretBytes)
            throw new InvalidOperationException($"{JwtSecretVariable} is required and must be at least {MinJwtSecretBytes} bytes.");

        return secret;
    }

    /// <summary>DEV_AUTH_ENABLED is a local-only opt-in: refuse it outside the Development environment.</summary>
    public static void ValidateDevAuth(IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = configuration.GetValue<bool>(DevAuthVariable);
        if (enabled && !environment.IsDevelopment())
            throw new InvalidOperationException($"{DevAuthVariable} is only allowed in the Development environment.");
    }
}
