using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for skills (PROFILE-01-02).
/// </summary>
public static class SkillEndpoints
{
    public static IEndpointRouteBuilder MapSkillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Skills")
                       .RequireAuthorization();

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
        var userId = ProfileEndpoints.GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return ProfileEndpoints.UnauthorizedIdentity();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.Name), ["Name is required."] },
            });

        if (dto.Name.Trim().Length > Skill.MaxNameLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(dto.Name), [$"Name must not exceed {Skill.MaxNameLength} characters."] },
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

        // Find or auto-create profile (PROFILE-01-01 prerequisite) — race-safe.
        var profile = await ProfileEndpoints.GetOrCreateProfileAsync(db, userId, logger);

        var skill = new Skill(profile.Id, dto.Name, dto.Proficiency, dto.YearsOfExperience);
        db.Skills.Add(skill);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Defense in depth: length and check-constraint violations return 400, not 500.
            logger.LogWarning(ex, "Rejected skill insert for profile {ProfileId}", profile.Id);
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { string.Empty, ["Invalid skill data. Check field lengths and proficiency range."] },
            });
        }

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
        var userId = ProfileEndpoints.GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return ProfileEndpoints.UnauthorizedIdentity();

        // Ownership is part of the query, so a missing skill and another user's skill take the same path.
        var skill = await db.Skills
            .FirstOrDefaultAsync(s => s.Id == id && s.Profile!.UserId == userId);

        if (skill is null)
            return ProfileEndpoints.NotFoundForCaller();

        db.Skills.Remove(skill);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted skill {SkillId} by user {UserId}", id, userId);

        return Results.NoContent();
    }
}
