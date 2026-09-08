using System.Security.Claims;

namespace Profile.Api.Middleware;

/// <summary>
/// DevAuthMiddleware — chỉ kích hoạt trong môi trường Development.
/// Đọc X-User-Id và X-User-Role từ request header và tổng hợp thành ClaimsPrincipal,
/// giả lập hành vi của YARP Gateway khi forward headers từ JWT đã validate (PROFILE-01, GW-01).
/// </summary>
public class DevAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DevAuthMiddleware> _logger;

    public DevAuthMiddleware(RequestDelegate next, ILogger<DevAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userIdStr = context.Request.Headers["X-User-Id"].FirstOrDefault();
        var userRole = context.Request.Headers["X-User-Role"].FirstOrDefault() ?? "Candidate";

        if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out var userId))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, userRole),
            };

            var identity = new ClaimsIdentity(claims, authenticationType: "DevHeader");
            context.User = new ClaimsPrincipal(identity);

            _logger.LogDebug(
                "DevAuthMiddleware: authenticated user {UserId} with role {Role}",
                userId, userRole);
        }

        await _next(context);
    }
}
