using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Profile.Api.Configuration;
using Profile.Api.Endpoints;
using Profile.Api.Middleware;
using Profile.Api.Observability;
using Profile.Core.Interfaces;
using Profile.Infrastructure.Data;
using Profile.Infrastructure.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// ── Logging — Serilog, compact JSON in non-Development (NFR MAINT, PBL6-49) ──
builder.AddServiceLogging("profile");

// Monitoring: expose OpenTelemetry metrics on /metrics (Prometheus scrape target).
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

// ── Database — Npgsql / EF Core 10 (REL-01, MAINT) ────────────────────────
var connStr = StartupConfiguration.GetDatabaseConnectionString(builder.Configuration);

builder.Services.AddDbContext<ProfileDbContext>(opts =>
    opts.UseNpgsql(connStr));
builder.Services.AddServiceHealthChecks<ProfileDbContext>();

// ── AES-256-GCM encryption (SEC-08) ───────────────────────────────────────
// The key is validated when the service is first resolved; Program resolves it
// before the app runs, so a missing or malformed key stops startup.
builder.Services.AddSingleton<IAesEncryptionService, AesGcmEncryptionService>();

// ── Authentication: identity comes only from a validated JWT (AGENTS.md, GW-01) ──
// The service is publicly reachable, so it validates tokens itself in every environment
// instead of trusting gateway-forwarded headers.
var jwtSecret = StartupConfiguration.GetJwtSecret(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "job-platform",
            ValidateAudience = true,
            ValidAudience = "job-platform",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

builder.Services.AddAuthorization();

// Header-based identity (DevAuthMiddleware) is an explicit local-only opt-in.
StartupConfiguration.ValidateDevAuth(builder.Configuration, builder.Environment);
var devAuthEnabled = builder.Configuration.GetValue<bool>(StartupConfiguration.DevAuthVariable);

// ── Problem Details RFC 7807 (REL-07) ─────────────────────────────────────
builder.Services.AddProblemDetails();

// ── Swagger / OpenAPI 3.0 ─────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Profile Service API",
        Version = "v1",
        Description = "User Profile CRUD, Skills, Work Experience, Education — PBL6 (PROFILE-01 to PROFILE-01-06, SEC-08)",
    });

    // Dev only: X-User-Id header, honoured only when DEV_AUTH_ENABLED=true in Development.
    opts.AddSecurityDefinition("DevHeader", new OpenApiSecurityScheme
    {
        Name = "X-User-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Requires DEV_AUTH_ENABLED=true (Development only). Pass a user UUID as the X-User-Id header.",
    });

    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter 'Bearer {token}'.",
    });

    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "DevHeader" },
            },
            []
        },
    });
});

// ── JSON serialisation ─────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Resolve the encryption service now so a missing or malformed key fails startup, not the first request.
_ = app.Services.GetRequiredService<IAesEncryptionService>();

// ── Auto-migrate on startup (REL-01) ──────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed. Service cannot start with inconsistent schema.");
        throw; // fail-fast
    }
}

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseServiceLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts =>
    {
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Profile Service v1");
        opts.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();

if (devAuthEnabled)
{
    app.UseMiddleware<DevAuthMiddleware>();
}

app.UseAuthorization();

// /metrics is meant for the internal scrape network; keep it off in Production
// unless METRICS_ENABLED=true (all services are publicly reachable).
if (app.Configuration.GetValue("METRICS_ENABLED", !app.Environment.IsProduction()))
{
    app.MapPrometheusScrapingEndpoint();
}

// ── Health check (REL-06) ─────────────────────────────────────────────────
// /health = static liveness, /health/ready = dependency checks (PBL6-49)
app.MapServiceHealth("profile");

// ── API Endpoints ─────────────────────────────────────────────────────────
app.MapProfileEndpoints();
app.MapSkillEndpoints();
app.MapExperienceEndpoints();
app.MapEducationEndpoints();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
