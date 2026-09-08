using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints quản lý kinh nghiệm làm việc — PROFILE-01-03.
/// </summary>
public static class ExperienceEndpoints
{
    public static IEndpointRouteBuilder MapExperienceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Experiences");

        group.MapPost("/experiences", AddExperience)
             .WithName("AddExperience")
             .WithSummary("Add a work experience entry (PROFILE-01-03).")
             .Produces<object>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/experiences/{id:guid}", UpdateExperience)
             .WithName("UpdateExperience")
             .WithSummary("Update a work experience entry (owner only, PROFILE-01-03).")
             .Produces<object>(StatusCodes.Status200OK)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/experiences/{id:guid}", DeleteExperience)
             .WithName("DeleteExperience")
             .WithSummary("Delete a work experience entry (owner only, PROFILE-01-03).")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // ── POST /api/profiles/experiences ───────────────────────────────────────
    private static async Task<IResult> AddExperience(
        ExperienceCreateDto dto,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var validationError = ValidateDto(dto.Company, dto.Title, dto.StartDate, dto.EndDate, dto.IsCurrent);
        if (validationError is not null)
            return validationError;

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile(userId, "Unknown");
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
        }

        var exp = new WorkExperience(
            profile.Id, dto.Company, dto.Title,
            dto.StartDate, dto.EndDate, dto.IsCurrent, dto.Description);
        db.WorkExperiences.Add(exp);
        await db.SaveChangesAsync();

        logger.LogInformation("Added experience {ExperienceId} to profile {ProfileId}", exp.Id, profile.Id);

        return Results.Created(
            $"/api/profiles/experiences/{exp.Id}",
            new { id = exp.Id, message = "Work experience added successfully" });
    }

    // ── PUT /api/profiles/experiences/{id} ───────────────────────────────────
    private static async Task<IResult> UpdateExperience(
        Guid id,
        ExperienceUpdateDto dto,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var validationError = ValidateDto(dto.Company, dto.Title, dto.StartDate, dto.EndDate, dto.IsCurrent);
        if (validationError is not null)
            return validationError;

        var exp = await db.WorkExperiences
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exp is null)
            return Results.NotFound(new { message = "Work experience not found." });

        if (exp.Profile is null || exp.Profile.UserId != userId)
            return Results.Problem("You are not allowed to update this work experience.", statusCode: StatusCodes.Status403Forbidden);

        exp.Update(dto.Company, dto.Title, dto.StartDate, dto.EndDate, dto.IsCurrent, dto.Description);
        await db.SaveChangesAsync();

        logger.LogInformation("Updated experience {ExperienceId} by user {UserId}", id, userId);

        return Results.Ok(new { id = exp.Id, message = "Work experience updated successfully" });
    }

    // ── DELETE /api/profiles/experiences/{id} ────────────────────────────────
    private static async Task<IResult> DeleteExperience(
        Guid id,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var exp = await db.WorkExperiences
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exp is null)
            return Results.NotFound(new { message = "Work experience not found." });

        if (exp.Profile is null || exp.Profile.UserId != userId)
            return Results.Problem("You are not allowed to delete this work experience.", statusCode: StatusCodes.Status403Forbidden);

        db.WorkExperiences.Remove(exp);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted experience {ExperienceId} by user {UserId}", id, userId);

        return Results.NoContent();
    }

    // ── Shared validation: StartDate <= EndDate when not current ─────────────
    private static IResult? ValidateDto(string company, string title, DateOnly startDate, DateOnly? endDate, bool isCurrent)
    {
        if (string.IsNullOrWhiteSpace(company))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(company), ["Company is required."] },
            });

        if (string.IsNullOrWhiteSpace(title))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(title), ["Title is required."] },
            });

        if (!isCurrent && endDate.HasValue && endDate.Value < startDate)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(endDate), ["EndDate must be >= StartDate when not current."] },
            });

        return null;
    }
}

