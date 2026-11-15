using System.Security.Claims;

namespace Profile.Api.Middleware;

/// <summary>
/// Local-development identity shortcut, registered only when DEV_AUTH_ENABLED=true in the
/// Development environment (see Program.cs). Builds a principal from the X-User-Id and
/// X-User-Role headers when the request is not already authenticated by a JWT.
/// </summary>
public class DevAuthMiddleware
{
    private const string DefaultRole = "Candidate";

    private readonly RequestDelegate _next;
    private readonly ILogger<DevAuthMiddleware> _logger;

    public DevAuthMiddleware(RequestDelegate next, ILogger<DevAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var userIdStr = context.Request.Headers["X-User-Id"].FirstOrDefault();
            var userRole = context.Request.Headers["X-User-Role"].FirstOrDefault() ?? DefaultRole;

            if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out var userId))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new(ClaimTypes.Role, userRole),
                };

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "DevHeader"));

                _logger.LogDebug(
                    "DevAuthMiddleware: authenticated user {UserId} with role {Role}",
                    userId, userRole);
            }
        }

        await _next(context);
    }
}
