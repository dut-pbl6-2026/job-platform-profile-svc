using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Core.Interfaces;
using Profile.Core.Validation;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for profile CRUD (PROFILE-01-01, PROFILE-01-05, PROFILE-01-06).
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Profiles")
                       .RequireAuthorization();

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

        // PROFILE-01-06: public profile by userId. Authenticated callers only; the DTO carries no PII.
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
        var userId = GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return UnauthorizedIdentity();

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.FullName), ["FullName is required."] },
            });

        if (!AvatarUrlPolicy.TryNormalize(dto.AvatarUrl, out var avatarUrl, out var avatarError))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.AvatarUrl), [avatarError!] },
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
            profile.Update(dto.FullName, dto.Headline, dto.Summary, avatarUrl, phoneEnc, addressEnc, dobEnc);
            db.Profiles.Add(profile);
            logger.LogInformation("Creating new profile for user {UserId}", userId);
        }
        else
        {
            profile.Update(dto.FullName, dto.Headline, dto.Summary, avatarUrl, phoneEnc, addressEnc, dobEnc);
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
        var userId = GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return UnauthorizedIdentity();

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

        // PublicProfileDto must not include phone, address or dateOfBirth (SEC-08)
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
    /// <summary>
    /// Placeholder FullName for a profile auto-created by POST skills/experiences/educations.
    /// The user should PUT /me to set a real name.
    /// </summary>
    internal const string AutoCreatedFullName = "Unknown";

    /// <summary>
    /// Finds the profile for userId, creating it if missing (prerequisite for POST skills/experiences/educations).
    /// Race-safe: when a concurrent request inserts the same user's profile first, the unique
    /// IX_profiles_user_id violation is caught and the existing row is read back instead of returning 500.
    /// </summary>
    internal static async Task<UserProfile> GetOrCreateProfileAsync(
        ProfileDbContext db,
        Guid userId,
        ILogger logger)
    {
        var existing = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (existing is not null)
            return existing;

        var profile = new UserProfile(userId, AutoCreatedFullName);
        db.Profiles.Add(profile);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent request inserted this user's profile first: read it back.
            db.ChangeTracker.Clear();
            var raced = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (raced is not null)
                return raced;
            throw;
        }

        logger.LogInformation("Auto-created profile {ProfileId} for user {UserId}", profile.Id, userId);
        return profile;
    }

    /// <summary>
    /// Returns the caller's user id from the authenticated principal (NameIdentifier claim),
    /// or Guid.Empty when the claim is missing or not a GUID. Request headers are never read.
    /// </summary>
    internal static Guid GetCurrentUserId(HttpContext context)
    {
        var claimValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out var userId) ? userId : Guid.Empty;
    }

    internal static IResult UnauthorizedIdentity() =>
        Results.Problem("Authenticated user id is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>
    /// The single response for a child row that is missing or owned by another user. Both cases
    /// return this identical 404, so a caller cannot probe which row ids exist.
    /// </summary>
    internal static IResult NotFoundForCaller() =>
        Results.NotFound(new { message = "Resource not found." });
}
