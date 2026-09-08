using Microsoft.EntityFrameworkCore;
using Profile.Core.DTOs;
using Profile.Core.Entities;
using Profile.Infrastructure.Data;

namespace Profile.Api.Endpoints;

/// <summary>
/// Minimal API endpoints quản lý học vấn — PROFILE-01-04.
/// </summary>
public static class EducationEndpoints
{
    public static IEndpointRouteBuilder MapEducationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profiles")
                       .WithTags("Educations");

        group.MapPost("/educations", AddEducation)
             .WithName("AddEducation")
             .WithSummary("Add an education entry (PROFILE-01-04).")
             .Produces<object>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/educations/{id:guid}", UpdateEducation)
             .WithName("UpdateEducation")
             .WithSummary("Update an education entry (owner only, PROFILE-01-04).")
             .Produces<object>(StatusCodes.Status200OK)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/educations/{id:guid}", DeleteEducation)
             .WithName("DeleteEducation")
             .WithSummary("Delete an education entry (owner only, PROFILE-01-04).")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> AddEducation(
        EducationCreateDto dto,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var err = ValidateDto(dto.Institution, dto.StartDate, dto.EndDate);
        if (err is not null) return err;

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile(userId, "Unknown");
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
        }

        var edu = new Education(profile.Id, dto.Institution, dto.Degree, dto.Field,
            dto.StartDate, dto.EndDate, dto.Grade);
        db.Educations.Add(edu);
        await db.SaveChangesAsync();

        return Results.Created($"/api/profiles/educations/{edu.Id}",
            new { id = edu.Id, message = "Education entry added successfully" });
    }

    private static async Task<IResult> UpdateEducation(
        Guid id,
        EducationUpdateDto dto,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var err = ValidateDto(dto.Institution, dto.StartDate, dto.EndDate);
        if (err is not null) return err;

        var edu = await db.Educations
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (edu is null)
            return Results.NotFound(new { message = "Education entry not found." });

        if (edu.Profile is null || edu.Profile.UserId != userId)
            return Results.Problem("You are not allowed to update this education entry.", statusCode: StatusCodes.Status403Forbidden);

        edu.Update(dto.Institution, dto.Degree, dto.Field, dto.StartDate, dto.EndDate, dto.Grade);
        await db.SaveChangesAsync();

        return Results.Ok(new { id = edu.Id, message = "Education entry updated successfully" });
    }

    private static async Task<IResult> DeleteEducation(
        Guid id,
        HttpContext context,
        ProfileDbContext db,
        ILogger<Program> logger)
    {
        var userId = ProfileEndpoints.GetUserId(context);
        if (userId == Guid.Empty)
            return Results.Problem("X-User-Id header is missing or invalid.", statusCode: StatusCodes.Status401Unauthorized);

        var edu = await db.Educations
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (edu is null)
            return Results.NotFound(new { message = "Education entry not found." });

        if (edu.Profile is null || edu.Profile.UserId != userId)
            return Results.Problem("You are not allowed to delete this education entry.", statusCode: StatusCodes.Status403Forbidden);

        db.Educations.Remove(edu);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static IResult? ValidateDto(string institution, DateOnly startDate, DateOnly? endDate)
    {
        if (string.IsNullOrWhiteSpace(institution))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(institution), ["Institution is required."] },
            });

        if (endDate.HasValue && endDate.Value < startDate)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(endDate), ["EndDate must be >= StartDate."] },
            });

        return null;
    }
}
