// Profile.Api — Program.cs (stub for PR #1 build verification)
// Full implementation in PR #2 (feat/profile-crud-api)

var builder = WebApplication.CreateBuilder(args);

// SEC-08 fail-fast: never boot non-Development without an encryption key.
// (Full DI wiring + auto-migrate land in PR #2; this keeps the stub honest
// so a missing key cannot silently boot prod on a dev fallback.)
var encKey = builder.Configuration["PROFILE_ENCRYPTION_KEY"]
             ?? builder.Configuration["ENCRYPTION_KEY"];
if (string.IsNullOrWhiteSpace(encKey) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "PROFILE_ENCRYPTION_KEY (or ENCRYPTION_KEY) is not configured. " +
        "Set it before deploying outside Development.");
}

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "profile" }));
app.Run();
