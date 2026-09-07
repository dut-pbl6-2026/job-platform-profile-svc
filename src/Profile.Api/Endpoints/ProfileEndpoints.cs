using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Core.Interfaces;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints cho Profile CRUD — PROFILE-01-01, PROFILE-01-05, PROFILE-01-06.
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Profiles");

        // PROFILE-01-01: Upsert profile of current authenticated user
        group.MapPut("/me", UpsertProfile)
             .WithName("UpsertProfile")
             .WithSummary("Create or update the current user's profile (PROFILE-01-01).")
             .Produces<object>(StatusCodes.Status200OK)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        // PROFILE-01-05: Get private profile (with decrypted PII) of current user
        group.MapGet("/me", GetMyProfile)
             .WithName("GetMyProfile")
             .WithSummary("Get the current user's full profile with decrypted PII (PROFILE-01-05).")
             .Produces<ProfileDetailDto>()
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound);

        // PROFILE-01-06: Get public profile by userId — NO PII
        group.MapGet("/{userId:guid}", GetPublicProfile)
             .WithName("GetPublicProfile")
             .WithSummary("Get a public profile by userId. PII fields excluded (PROFILE-01-06, SEC-08).")
             .Produces<PublicProfileDto>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // ── PUT /api/profiles/me ─────────────────────────────────────────────────
    private static async Task<IResult> UpsertProfile(
        ProfileUpdateDto dto,
        HttpContext context,
        ProfileDbContext db,
        IAesEncryptionService aes,
        ILogger<Program> logger)
    {
        var userId = GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.FullName), ["FullName is required."] },
            });

        // Encrypt PII fields (SEC-08)
        var phoneEnc = aes.Encrypt(dto.Phone);
        var addressEnc = aes.Encrypt(dto.Address);
        var dobEnc = dto.DateOfBirth.HasValue
            ? aes.Encrypt(dto.DateOfBirth.Value.ToString("yyyy-MM-dd"))
            : null;

        var profile = await db.Profiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            profile = new UserProfile(userId, dto.FullName);
            profile.Update(dto.FullName, dto.Headline, dto.Summary, dto.AvatarUrl, phoneEnc, addressEnc, dobEnc);
            db.Profiles.Add(profile);
            logger.LogInformation("Creating new profile for user {UserId}", userId);
        }
        else
        {
            profile.Update(dto.FullName, dto.Headline, dto.Summary, dto.AvatarUrl, phoneEnc, addressEnc, dobEnc);
            logger.LogInformation("Updating profile {ProfileId} for user {UserId}", profile.Id, userId);
        }

        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Profile updated successfully", id = profile.Id });
    }

    // ── GET /api/profiles/me ─────────────────────────────────────────────────
    private static async Task<IResult> GetMyProfile(
        HttpContext context,
        ProfileDbContext db,
        IAesEncryptionService aes)
    {
        var userId = GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var profile = await db.Profiles
            .Include(p => p.Skills)
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
            return Results.NotFound(new { message = "Profile not found." });

        // Decrypt PII for the profile owner (SEC-08)
        var dto = new ProfileDetailDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            FullName = profile.FullName,
            Headline = profile.Headline,
            Summary = profile.Summary,
            AvatarUrl = profile.AvatarUrl,
            Phone = aes.Decrypt(profile.PhoneEncrypted),
            Address = aes.Decrypt(profile.AddressEncrypted),
            DateOfBirth = aes.Decrypt(profile.DateOfBirthEncrypted),
            Skills = profile.Skills.Select(s => new SkillDto
            {
                Id = s.Id,
                Name = s.Name,
                Proficiency = s.Proficiency,
                YearsOfExperience = s.YearsOfExperience,
            }).ToList(),
            Experiences = profile.Experiences.Select(e => new WorkExperienceDto
            {
                Id = e.Id,
                Company = e.Company,
                Title = e.Title,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsCurrent = e.IsCurrent,
                Description = e.Description,
            }).ToList(),
            Educations = profile.Educations.Select(e => new EducationDto
            {
                Id = e.Id,
                Institution = e.Institution,
                Degree = e.Degree,
                Field = e.Field,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Grade = e.Grade,
            }).ToList(),
        };

        return Results.Ok(dto);
    }

    // ── GET /api/profiles/{userId} ────────────────────────────────────────────
    private static async Task<IResult> GetPublicProfile(
        Guid userId,
        ProfileDbContext db)
    {
        var profile = await db.Profiles
            .Include(p => p.Skills)
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
            return Results.NotFound(new { message = "Profile not found." });

        // PublicProfileDto MUST NOT include phone, address, dateOfBirth (SEC-08)
        var dto = new PublicProfileDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            FullName = profile.FullName,
            Headline = profile.Headline,
            Summary = profile.Summary,
            AvatarUrl = profile.AvatarUrl,
            Skills = profile.Skills.Select(s => new SkillDto
            {
                Id = s.Id,
                Name = s.Name,
                Proficiency = s.Proficiency,
                YearsOfExperience = s.YearsOfExperience,
            }).ToList(),
            Experiences = profile.Experiences.Select(e => new WorkExperienceDto
            {
                Id = e.Id,
                Company = e.Company,
                Title = e.Title,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsCurrent = e.IsCurrent,
                Description = e.Description,
            }).ToList(),
            Educations = profile.Educations.Select(e => new EducationDto
            {
                Id = e.Id,
                Institution = e.Institution,
                Degree = e.Degree,
                Field = e.Field,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Grade = e.Grade,
            }).ToList(),
        };

        return Results.Ok(dto);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    internal static Guid GetUserId(HttpContext context)
    {
        // Try Claims first (set by DevAuthMiddleware or JWT Bearer)
        var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(claimValue) && Guid.TryParse(claimValue, out var claimGuid))
            return claimGuid;

        // Fallback: read X-User-Id header directly (gateway forward pattern)
        var headerValue = context.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerValue) && Guid.TryParse(headerValue, out var headerGuid))
            return headerGuid;

        return Guid.Empty;
    }
}
