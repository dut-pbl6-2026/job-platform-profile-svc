using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints quản lý kỹ năng — PROFILE-01-02.
/// </summary>
public static class SkillEndpoints
{
    public static IEndpointRouteBuilder MapSkillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Skills");

        // PROFILE-01-02: Add skill to current user's profile
        group.MapPost("/skills", AddSkill)
             .WithName("AddSkill")
             .WithSummary("Add a skill to the current user's profile (PROFILE-01-02).")
             .Produces<object>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        // PROFILE-01-02: Delete skill (owner only)
        group.MapDelete("/skills/{id:guid}", DeleteSkill)
             .WithName("DeleteSkill")
             .WithSummary("Delete a skill (owner only, PROFILE-01-02).")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // ── POST /api/profiles/skills ────────────────────────────────────────────
    private static async Task<IResult> AddSkill(
        SkillCreateDto dto,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.Name), ["Name is required."] },
            });

        if (dto.Proficiency is < 1 or > 5)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.Proficiency), ["Proficiency must be between 1 and 5."] },
            });

        if (dto.YearsOfExperience < 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.YearsOfExperience), ["YearsOfExperience must be >= 0."] },
            });

        // Find or auto-create profile (PROFILE-01-01 prerequisite)
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile(userId, "Unknown");
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
            logger.LogInformation("Auto-created profile {ProfileId} for user {UserId}", profile.Id, userId);
        }

        var skill = new Skill(profile.Id, dto.Name, dto.Proficiency, dto.YearsOfExperience);
        db.Skills.Add(skill);
        await db.SaveChangesAsync();

        logger.LogInformation("Added skill {SkillId} to profile {ProfileId}", skill.Id, profile.Id);

        return Results.Created(
            $"/api/profiles/skills/{skill.Id}",
            new { id = skill.Id, message = "Skill added successfully" });
    }

    // ── DELETE /api/profiles/skills/{id} ─────────────────────────────────────
    private static async Task<IResult> DeleteSkill(
        Guid id,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var skill = await db.Skills
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (skill is null)
            return Results.NotFound(new { message = "Skill not found." });

        // Ownership check — only the profile owner can delete (403 otherwise)
        if (skill.Profile is null || skill.Profile.UserId != userId)
            return Results.Problem("You are not allowed to delete this skill.", statusCode: StatusCodes.Status403Forbidden);

        db.Skills.Remove(skill);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted skill {SkillId} by user {UserId}", id, userId);

        return Results.NoContent();
    }
}
