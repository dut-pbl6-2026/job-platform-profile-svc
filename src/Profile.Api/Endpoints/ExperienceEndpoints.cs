using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for work experience (PROFILE-01-03).
/// </summary>
public static class ExperienceEndpoints
{
    public static IEndpointRouteBuilder MapExperienceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Experiences")
                       .RequireAuthorization();

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
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/experiences/{id:guid}", DeleteExperience)
             .WithName("DeleteExperience")
             .WithSummary("Delete a work experience entry (owner only, PROFILE-01-03).")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
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
        var userId = ProfileEndpoints.GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return ProfileEndpoints.UnauthorizedIdentity();

        var validationError = ValidateDto(dto.Company, dto.Title, dto.Description, dto.StartDate, dto.EndDate, dto.IsCurrent);
        if (validationError is not null)
            return validationError;

        var profile = await ProfileEndpoints.GetOrCreateProfileAsync(db, userId, logger);

        var exp = new WorkExperience(
            profile.Id, dto.Company, dto.Title,
            dto.StartDate, dto.EndDate, dto.IsCurrent, dto.Description);
        db.WorkExperiences.Add(exp);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Defense in depth: length violations return 400, not 500.
            logger.LogWarning(ex, "Rejected experience insert for profile {ProfileId}", profile.Id);
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { string.Empty, ["Invalid work experience data. Check field lengths and dates."] },
            });
        }

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
        var userId = ProfileEndpoints.GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return ProfileEndpoints.UnauthorizedIdentity();

        var validationError = ValidateDto(dto.Company, dto.Title, dto.Description, dto.StartDate, dto.EndDate, dto.IsCurrent);
        if (validationError is not null)
            return validationError;

        var exp = await db.WorkExperiences
            .FirstOrDefaultAsync(e => e.Id == id && e.Profile!.UserId == userId);

        if (exp is null)
            return ProfileEndpoints.NotFoundForCaller();

        exp.Update(dto.Company, dto.Title, dto.StartDate, dto.EndDate, dto.IsCurrent, dto.Description);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Rejected experience update {ExperienceId}", id);
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { string.Empty, ["Invalid work experience data. Check field lengths and dates."] },
            });
        }

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
        var userId = ProfileEndpoints.GetCurrentUserId(context);
        if (userId == Guid.Empty)
            return ProfileEndpoints.UnauthorizedIdentity();

        var exp = await db.WorkExperiences
            .FirstOrDefaultAsync(e => e.Id == id && e.Profile!.UserId == userId);

        if (exp is null)
            return ProfileEndpoints.NotFoundForCaller();

        db.WorkExperiences.Remove(exp);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted experience {ExperienceId} by user {UserId}", id, userId);

        return Results.NoContent();
    }

    // ── Shared validation: required + MaxLength + StartDate <= EndDate when not current ──
    private static IResult? ValidateDto(string company, string title, string? description, DateOnly startDate, DateOnly? endDate, bool isCurrent)
    {
        if (string.IsNullOrWhiteSpace(company))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(company), ["Company is required."] },
            });

        if (company.Trim().Length > WorkExperience.MaxCompanyLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(company), [$"Company must not exceed {WorkExperience.MaxCompanyLength} characters."] },
            });

        if (string.IsNullOrWhiteSpace(title))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(title), ["Title is required."] },
            });

        if (title.Trim().Length > WorkExperience.MaxTitleLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(title), [$"Title must not exceed {WorkExperience.MaxTitleLength} characters."] },
            });

        if (description is not null && description.Trim().Length > WorkExperience.MaxDescriptionLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(description), [$"Description must not exceed {WorkExperience.MaxDescriptionLength} characters."] },
            });

        if (!isCurrent && endDate.HasValue && endDate.Value < startDate)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(endDate), ["EndDate must be >= StartDate when not current."] },
            });

        return null;
    }
}

