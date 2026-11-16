# AGENTS — job-platform-profile-svc

> Candidate & User Profile microservice. SRS: `job-platform-docs/docs/master-plan.md:181`, `docs/srs/en/{3-must-have-fr:PROFILE-01,8-system-architecture,6-nfr:SEC-08,REL-01,MAINT}` (and `docs/srs/vi/`). Git: `job-platform-docs/.github/git-strategy.md` (`feature/* → main`).

## Mise activation

Activate `mise` for bare `dotnet`/`infisical` without `mise exec`:

| Shell | Add to config file | Activate |
|-------|--------------------|----------|
| `bash` | `~/.bashrc` or `~/.bash_profile` | `eval "$(mise activate bash)"` |
| `zsh` | `~/.zshrc` | `eval "$(mise activate zsh)"` |
| `fish` | `~/.config/fish/config.fish` | `mise activate fish \| source` |
| `PowerShell` | `$PROFILE` | `mise activate pwsh \| Out-String \| Invoke-Expression` |

Agent uses `mise exec -- dotnet ...` / `mise exec -- infisical ...` due to non-interactive shell without `mise activate`; humans just use `dotnet` / `infisical` after `mise install`.

## Scope

`PBL6-3` MUST `PROFILE-01` — User Profile CRUD, Skills, Work Experience, Education management, `Port 5005` `net10.0` `YARP gateway`. Owner TM2 W3 (Sprint Schedule: Day Tue W3). DB `job_platform_profile`.

## Architecture — clean Api/Core/Infrastructure

```
src/Profile.Api            → Web API (Program.cs JWT Bearer + DevAuth + Swagger + /health; `migrate` one-off command)
src/Profile.Core           → Domain (UserProfile, Skill, WorkExperience, Education : Entity) + IAesEncryptionService + DTOs
src/Profile.Infrastructure → Data (ProfileDbContext Npgsql, Migrations) + Services (AesGcmEncryptionService)
tests/Profile.Tests        → xunit (ProfileCrudTests, SkillExperienceTests, AesEncryptionTests, AuthorizationTests)
ProfileService.sln         → mise run build/test
```

Dependency: `Api → Infrastructure → Core → SharedKernel` (`PackageReference JobPlatform.SharedKernel 0.4.0` via the GitHub Packages feed in `nuget.config`, never `ProjectReference` per `master-plan.md:132`). `MAINT-01` clean arch, `Result<T>` for domain failures not exceptions.

## SRS mapping (PROFILE-01)

- `GET /api/profiles/me` (`PROFILE-01-05`) — View private profile of current authenticated user. Returns `ProfileDetailDto` with sensitive PII (`phone`, `address`, `date_of_birth`) decrypted in application layer.
- `PUT /api/profiles/me` (`PROFILE-01-01`) — Upsert profile (`fullName`, `phone`, `address`, `headline`, `summary`, `avatarUrl`, `dateOfBirth`). Sensitive fields encrypted with AES-256-GCM before persisting. `avatarUrl` must be an absolute `https` URL without user information, at most 512 characters, with no whitespace or control characters; anything else (`javascript:`, `data:`, `http:`, relative) returns `400`. Empty or whitespace clears the avatar.
- `GET /api/profiles/{id}` (`PROFILE-01-06`) — View public profile by user ID. Returns `PublicProfileDto` with sensitive fields (`phone`, `address`, `date_of_birth`) strictly excluded/hidden.
- `POST /api/profiles/skills` (`PROFILE-01-02`) — Add skill (`name`, `proficiency` 1-5, `yearsOfExperience`). Domain and DB check constraint enforces `proficiency BETWEEN 1 AND 5`.
- `DELETE /api/profiles/skills/{skillId}` (`PROFILE-01-02`) — Delete skill (owner only).
- `POST /api/profiles/experiences` (`PROFILE-01-03`) — Add work experience (`companyName`, `position`, `startDate`, `endDate`, `isCurrent`, `description`). Validation: `endDate >= startDate` unless `isCurrent == true`.
- `PUT /api/profiles/experiences/{expId}` (`PROFILE-01-03`) — Update work experience (owner only).
- `DELETE /api/profiles/experiences/{expId}` (`PROFILE-01-03`) — Delete work experience (owner only).
- `POST /api/profiles/educations` (`PROFILE-01-04`) — Add education entry (`institution`, `degree`, `fieldOfStudy`, `startDate`, `endDate`, `grade`).
- `PUT /api/profiles/educations/{eduId}` (`PROFILE-01-04`) — Update education entry (owner only).
- `DELETE /api/profiles/educations/{eduId}` (`PROFILE-01-04`) — Delete education entry (owner only).
- Gateway `GW-01` routes `/api/profiles/*` → Profile Service after validating the JWT (`Authenticated` policy). The profile service validates the same JWT itself.

## Authentication & Authorization

Dual-mode authentication matching platform standard:
- **JWT Bearer (every environment)**: `Authorization: Bearer <token>` is validated in-service (`Auth/JwtBearerRs256.cs`: RS256 only, `ValidateIssuer/Audience=job-platform`, `ValidateLifetime`, `ClockSkew=Zero`, `kid` and `sub` required). Keys come from auth-svc's JWKS via `Jwt__MetadataAddress`. Startup fails if it is missing.
- **Identity**: Only the `ClaimTypes.NameIdentifier` claim of the validated token is used. Request headers (`X-User-Id`, `X-User-Role`) are never trusted for identity.
- **DevAuthMiddleware (local opt-in)**: Registered only when `DEV_AUTH_ENABLED=true` and the environment is `Development`; startup fails if the flag is set anywhere else. It builds a principal from `X-User-Id` / `X-User-Role` only for requests that are not already authenticated.
- All `/api/profiles/*` endpoints require authentication, including the public `GET /api/profiles/{userId}` (PROFILE-01-06), which returns the PII-free DTO.
- Ownership checks: `/me` and the write endpoints act on the caller's own profile, skills, experiences, and educations. Another user's skill, experience or education row returns the same `404 Not Found` as a missing row, so the response does not reveal which row ids exist.

## Security (SRS 6 `SEC-*`)

- `SEC-08 Sensitive Data Encryption` — Application-layer `AES-256-GCM` via `AesGcmEncryptionService`. Sensitive fields (`phone`, `address`, `date_of_birth`) encrypted before saving to DB and decrypted upon retrieval by owner.
  - Per-record 12-byte random Nonce (`RandomNumberGenerator.GetBytes(12)`).
  - Storage format (versioned): `Base64(KeyId[1B] + Nonce[12B] + Tag[16B] + Ciphertext[NB])`. The key id selects the decryption key, so the key can rotate.
  - Active key read from the `PROFILE_ENCRYPTION_KEY` env var: a base64-encoded 32-byte (256-bit) key used directly as the AES key. Generate one with `openssl rand -base64 32`. A missing, non-base64, or wrong-length key stops startup in every environment.
  - Key id of the active key: `PROFILE_ENCRYPTION_KEY_ID` (optional, `1`-`255`, default `1`).
  - Decrypt-only keys: `PROFILE_ENCRYPTION_KEYS_RETIRED` (optional), comma-separated `id:base64key` entries. It may hold any key that is not active: a key announced ahead of a switch, or a key retired after one. Ids must be unique and must differ from the active id. Records with an id that is not configured fail to decrypt. Never reuse an id.
  - Rotation (rolling replicas, no record rewrite):
    1. Put the new key in `PROFILE_ENCRYPTION_KEYS_RETIRED` as `<new id>:<new key>` and deploy to every replica. The active key does not change.
    2. Make it active: set `PROFILE_ENCRYPTION_KEY=<new key>` and `PROFILE_ENCRYPTION_KEY_ID=<new id>`, remove the `<new id>` entry from `PROFILE_ENCRYPTION_KEYS_RETIRED`, and add `<old id>:<old key>` there. Deploy.
    Records written before step 2 still decrypt. Re-encrypting old rows is not implemented, so the old key stays configured while any record still uses it.
- `SEC-05 SQLi` — Always use EF Core parameterized queries, never raw string concatenation.
- `SEC-05 CSRF` — Stateless API; CSRF N/A. Enforce `Content-Type: application/json` on mutation endpoints.
- `SEC-06 Rate limiting` — 100 req/min per IP/user enforced at Gateway level (`GW-01`).
- `SEC-10 CORS` — Trusted origins enforced by API Gateway; no local CORS configuration in profile-svc.

## Reliability (NFR `REL-01`, `REL-06`)

- Schema migration is a separate deploy step: `dotnet Profile.Api.dll migrate` (`Bootstrap/MigrateCommand.cs`, compose service `profile-migrate`) exits non-zero on failure. The web host never migrates on startup, so it cannot run against an unmigrated schema once the compose ordering is in place.
- DB connection pooling: `MaxPoolSize=20` configured via connection string `DATABASE_URL_PROFILE`.
- Health check: `GET /health` returns `{"status":"ok","service":"profile"}`.

## Data — EF Core (NFR `6-nfr.md:MAINT`)

- `ProfileDbContext`: `DbSet<UserProfile>`, `DbSet<Skill>`, `DbSet<WorkExperience>`, `DbSet<Education>`.
- Fluent API configurations:
  - Table names: `profiles`, `skills`, `work_experiences`, `educations`.
  - Columns: `profiles.phone`, `profiles.address`, `profiles.date_of_birth` stored as ciphertext string.
  - Constraints: `proficiency BETWEEN 1 AND 5` check constraint on `skills`.
  - Unique index: `IX_profiles_user_id` on `profiles(user_id)`.
  - Foreign keys: Cascade delete from `UserProfile` to child entities (`Skill`, `WorkExperience`, `Education`).
- Migrations located at `src/Profile.Infrastructure/Data/Migrations/` — verified via `mise run ef-check`.

## API Response Standards (7-eir.md:7.7)

**HTTP status codes**:
- `200 OK` — Profile details / list
- `201 Created` — Skill / Experience / Education created
- `204 No Content` — Deleted / Updated successfully without body
- `400 Bad Request` — Invalid input
- `401 Unauthorized` — Missing or invalid authentication
- `403 Forbidden` — Reserved for permission failures. Profile routes have no 403 path: `/me` is self-scoped and `GET /api/profiles/{userId}` is public.
- `404 Not Found` — Profile or entity not found, or another user's skill, experience or education row (the same response for both cases)
- `422 Unprocessable Entity` — Business validation failure (e.g. invalid date ranges, proficiency outside 1-5)
- `500 Internal Server Error` — Unhandled error

**Error response format** (RFC 7807 `ProblemDetails` via `UseExceptionHandler()` + `ILogger`):
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Bad Request",
  "status": 400,
  "detail": "Proficiency must be between 1 and 5.",
  "instance": "/api/profiles/skills"
}
```

## No hard-coding (STRICT — apply to every file you touch)

**NEVER** embed literal values for any of the following in source code (`.cs`, `.json`, `.yaml`, `.toml`, …):

| Category | Examples of forbidden literals |
|----------|--------------------------------|
| Connection strings | `Host=localhost;Port=5432;Database=job_platform_profile;...` |
| Ports / URLs | `http://localhost:5005`, `5005` |
| Secrets / passwords | plain-text keys, JWT secret, encryption keys |
| Database names | `job_platform_profile` (except in migrations) |

**Always** read from `IConfiguration` / environment variables:
```csharp
var conn = builder.Configuration.GetConnectionString("ProfileDb")
           ?? builder.Configuration["DATABASE_URL_PROFILE"]
           ?? throw new InvalidOperationException(
               "Connection string not configured. Set DATABASE_URL_PROFILE or ConnectionStrings:ProfileDb.");
```

- `appsettings.json` and `appsettings.Development.json` MUST NOT contain connection strings or credentials. The database connection string is read from `DATABASE_URL_PROFILE` (or `ConnectionStrings__ProfileDb`) in every environment, and startup fails when it is missing or blank.
- The single source of truth for all env values is `../job-platform-infra/envs/.env.dev.example` — use `mise run sync-env` to sync.
- Required env vars for this service (every environment): `DATABASE_URL_PROFILE`, `Jwt__MetadataAddress`, `PROFILE_ENCRYPTION_KEY` (base64 of 32 random bytes).
- Optional env vars: `PROFILE_ENCRYPTION_KEY_ID` (active key id, default `1`), `PROFILE_ENCRYPTION_KEYS_RETIRED` (decrypt-only keys, see SEC-08), `METRICS_ENABLED`, `DEV_AUTH_ENABLED` (Development only).
- Local development (`dotnet run` does not read `.env`): export the required variables in the shell first, for example `export DATABASE_URL_PROFILE='Host=localhost;Port=5432;Database=job_platform_profile;Username=postgres;Password=postgres'`. The same names are used by the dev compose file.
- Optional local-only env var: `DEV_AUTH_ENABLED=true` (Development only) to authenticate with `X-User-Id` instead of a token.

## 2026 best practice (NFR `MAINT`)

- `dotnet 10.0.100` `net10.0` `nullable enable` `ImplicitUsings` file-scoped namespace, `ProblemDetails` + `UseExceptionHandler` + `ILogger` JSON `ERROR/WARN/INFO/DEBUG`, `GET /health` per `8-system-architecture.md`.
- `dotnet build --warnaserror` + `dotnet format --verify-no-changes` (mise `build/test/format`), `EF` alignment `EF10.0.4` + `Npgsql10.0.3`, test coverage `>70%` `MAINT-02`.
- Never commit `.env` (`.gitignore`), `mise run sync-env` single source `../job-platform-infra/envs/.env.dev.example`.

## Workflow

```bash
mise trust && mise install
mise run sync-env && mise run verify
mise run build && mise run test && mise run format
mise run ef-check
mise run run  # http://localhost:5005/health → {"status":"ok","service":"profile"}
```

## Git convention (git-strategy.md)

Branch: `feature/<description>` | `bugfix/<description>` | `hotfix/v<semver>-<desc>` → `main`.

Commits — `<type>(profile): <subject>` (scope always `profile` for this repo):

| Type | Example |
|------|---------|
| `feat` | `feat(profile): add UserProfile CRUD endpoints` |
| `fix` | `fix(profile): decrypt phone before returning profile dto` |
| `refactor` | `refactor(profile): extract AesGcm helper methods` |
| `test` | `test(profile): add AES-256-GCM encryption roundtrip tests` |
| `docs` | `docs(profile): update AGENTS.md with endpoint specs` |
| `chore` | `chore(profile): update SharedKernel package` |
| `ci` | `ci(profile): configure github action workflow` |

PR checklist: Description / How to verify / Checklist `mise run build/test/format/ef-check`.
