using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Profile.Api.Endpoints;
using Profile.Api.Middleware;
using Profile.Core.Interfaces;
using Profile.Infrastructure.Data;
using Profile.Infrastructure.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── Logging — Structured JSON console (NFR MAINT) ─────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(opts =>
{
    opts.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

// ── Database — Npgsql / EF Core 10 (REL-01, MAINT) ────────────────────────
var connStr = builder.Configuration.GetConnectionString("ProfileDb")
              ?? builder.Configuration["DATABASE_URL_PROFILE"]
              ?? throw new InvalidOperationException(
                  "Connection string not configured. Set DATABASE_URL_PROFILE or ConnectionStrings:ProfileDb.");

builder.Services.AddDbContext<ProfileDbContext>(opts =>
    opts.UseNpgsql(connStr));

// ── AES-256-GCM Encryption Service (SEC-08) ───────────────────────────────
// Fail-fast in non-Development if encryption key is missing
var encKey = builder.Configuration["PROFILE_ENCRYPTION_KEY"]
             ?? builder.Configuration["ENCRYPTION_KEY"];

if (string.IsNullOrWhiteSpace(encKey) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "PROFILE_ENCRYPTION_KEY is required in non-Development environments (SEC-08).");
}

builder.Services.AddSingleton<IAesEncryptionService, AesGcmEncryptionService>();

// ── Authentication ─────────────────────────────────────────────────────────
if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
{
    var jwtSecret = builder.Configuration["JWT_SECRET"]
                    ?? throw new InvalidOperationException("JWT_SECRET is required in Production.");

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
}

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

    // Dev auth: allow passing X-User-Id & X-User-Role headers directly in Swagger UI
    opts.AddSecurityDefinition("DevHeader", new OpenApiSecurityScheme
    {
        Name = "X-User-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Dev mode: Pass your User UUID as X-User-Id header (no JWT required in Development).",
    });

    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Production: Enter 'Bearer {token}'.",
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
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts =>
    {
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Profile Service v1");
        opts.RoutePrefix = "swagger";
    });

    // DevAuthMiddleware: simulate gateway header forwarding in Development (no JWT required)
    app.UseMiddleware<DevAuthMiddleware>();
}
else
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// ── Health check (REL-06) ─────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "profile" }))
   .WithTags("Health")
   .WithName("HealthCheck")
   .ExcludeFromDescription();

// ── API Endpoints ─────────────────────────────────────────────────────────
app.MapProfileEndpoints();
app.MapSkillEndpoints();
app.MapExperienceEndpoints();
app.MapEducationEndpoints();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
