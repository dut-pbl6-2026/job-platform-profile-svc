// Profile.Api — Program.cs (stub for PR #1 build verification)
// Full implementation in PR #2 (feat/profile-crud-api)

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "profile" }));
app.Run();
