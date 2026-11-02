using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Profile.Api.Observability;

/// <summary>
/// PBL6-49: <c>/health</c> stays a static liveness probe;
/// <c>/health/ready</c> runs short-timeout dependency checks.
/// DB failure = Unhealthy (503). profile-svc has no Kafka/SMTP dependency, so only
/// the database is checked.
/// </summary>
public static class ServiceHealth
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddServiceHealthChecks<TDb>(
        this IServiceCollection services)
        where TDb : DbContext
    {
        var checks = services.AddHealthChecks();
        checks.AddCheck<DbContextHealthCheck<TDb>>("database", HealthStatus.Unhealthy, [ReadyTag]);

        return services;
    }

    /// <summary>Maps <c>/health</c> (unchanged JSON contract) and <c>/health/ready</c>.</summary>
    public static IEndpointRouteBuilder MapServiceHealth(this IEndpointRouteBuilder app, string service)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service }))
            .WithTags("Health")
            .WithName("HealthCheck")
            .ExcludeFromDescription();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadyTag),
            ResponseWriter = (ctx, report) => WriteReport(ctx, report, service)
        }).ExcludeFromDescription();

        return app;
    }

    private static Task WriteReport(HttpContext ctx, HealthReport report, string service)
    {
        ctx.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            service,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                durationMs = (long)e.Value.Duration.TotalMilliseconds
            })
        };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}

/// <summary>Opens a connection through the DbContext with a short timeout. Never leaks exception text (connection strings).</summary>
public sealed class DbContextHealthCheck<TDb>(TDb db) : IHealthCheck where TDb : DbContext
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);
        try
        {
            return await db.Database.CanConnectAsync(cts.Token)
                ? HealthCheckResult.Healthy("database reachable")
                : new HealthCheckResult(context.Registration.FailureStatus, "database unreachable");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "database check timed out");
        }
        catch (Exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "database unreachable");
        }
    }
}
