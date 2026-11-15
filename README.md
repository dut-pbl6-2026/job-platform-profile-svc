# job-platform-profile-svc

Candidate & User Profile microservice for **Vietnam Job Platform** (`pbl6`) — `dut-pbl6-2026`. `Profile CRUD` `Skills` `WorkExperience` `Education` `AES-256-GCM (SEC-08)`, `Port 5005` `net10.0`.

- Tech: .NET 10 Web API (`net10.0`, C# 13, Clean Architecture)
- Branch flow: `feature/* → main` (see `job-platform-docs/.github/git-strategy.md`)
- Jira: Epic `PBL6-3` (Profile & Candidate Domain, 5 pts, Day Tue W3)
- TM: TM1 Hoai, TM2 Thanh (Owner), TM3 Chi Bao, TM4 Khoa

## Overview

- `src/Profile.Api` — Web API Minimal APIs, `Swagger`, `/health`, `migrate` command, `DevAuthMiddleware`
- `src/Profile.Core` — Domain entities (`UserProfile`, `Skill`, `WorkExperience`, `Education` `: Entity`), `IAesEncryptionService`, DTOs
- `src/Profile.Infrastructure` — `ProfileDbContext` (`Npgsql`), EF Migrations, `AesGcmEncryptionService` (SEC-08)
- `tests/Profile.Tests` — `xunit` unit and integration tests
- `local-feed` — `JobPlatform.SharedKernel 0.1.0` via `nuget.config`

## Prerequisites

- `mise` https://mise.jdx.dev
- `docker` + `docker compose v2`
- `git` + `gh` (`gh auth login`)
- `dotnet 10.0.100` via `mise` — `mise trust && mise install`

See `AGENTS.md` for shell activation (`mise activate`) and agent `mise exec` notes.

## Clone

```bash
mkdir -p ~/projects/personal/job-platform && cd ~/projects/personal/job-platform
for r in infra shared auth-svc job-svc profile-svc; do gh repo clone dut-pbl6-2026/job-platform-$r; done
cd job-platform-profile-svc
```

## Setup

```bash
mise trust && mise install
mise run sync-env
mise run verify
cat .env | grep DATABASE_URL_PROFILE
```

Env single source: `../job-platform-infra/envs/.env.dev.example` → `.env` via `mise run sync-env`.

## Build & Test

```bash
mise run build     # dotnet build ProfileService.sln --warnaserror
mise run test      # dotnet test ProfileService.sln
mise run format    # dotnet format --verify-no-changes
mise run ef-check  # check for pending model changes
```

Update SharedKernel: `mise run pack-shared`.

## Health, logging & code quality
- `GET /health` - static liveness (`{"status":"ok","service":"..."}`).
- `GET /health/ready` - readiness with short-timeout dependency checks: database (503 when unreachable).
- Logging: Serilog, compact JSON on the console outside Development, request logging, `Service` + `CorrelationId` properties. `X-Correlation-Id` is reused if sent (and well-formed), otherwise generated, and always echoed in the response. Headers and bodies are never logged.
- SonarCloud (optional): CI runs `dotnet-sonarscanner` only when the `SONAR_TOKEN` secret is available (skipped on fork PRs). Optional repo variables `SONAR_ORGANIZATION` (default `dut-pbl6-2026`) and `SONAR_PROJECT_KEY` (default `dut-pbl6-2026_job-platform-profile-svc`).

## Run

```bash
mise run run              # dotnet run --project src/Profile.Api
curl http://localhost:5005/health   # {"status":"ok","service":"profile"}
```

Sample requests (set `TOKEN` to a JWT issued by auth-svc):

### Upsert profile (Candidate)
```bash
curl -X PUT http://localhost:5005/api/profiles/me \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "fullName": "Nguyen Van A",
    "phone": "0901234567",
    "address": "Da Nang, Vietnam",
    "headline": "Senior Full-stack Developer",
    "summary": "5+ years developing cloud-native solutions in .NET and React",
    "avatarUrl": "https://example.com/avatar.jpg",
    "dateOfBirth": "1998-05-15"
  }'
```

### View private profile (with decrypted PII)
```bash
curl http://localhost:5005/api/profiles/me \
  -H "Authorization: Bearer $TOKEN"
```

### View public profile (PII excluded; requires a token)
```bash
curl http://localhost:5005/api/profiles/a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d \
  -H "Authorization: Bearer $TOKEN"
```

### Add skill
```bash
curl -X POST http://localhost:5005/api/profiles/skills \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name": "C# / .NET", "proficiency": 5, "yearsOfExperience": 4}'
```

### Add work experience
```bash
curl -X POST http://localhost:5005/api/profiles/experiences \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "companyName": "Tech Corp",
    "position": "Software Engineer",
    "startDate": "2022-01-01",
    "isCurrent": true,
    "description": "Leading microservice migration"
  }'
```

## Authentication

Every `/api/profiles` request must carry a JWT issued by auth-svc: `Authorization: Bearer <token>`. `JWT_SECRET` (≥ 32 bytes) must match the issuer's secret. Identity comes only from the token; `X-User-Id` headers are ignored unless `DEV_AUTH_ENABLED=true` in Development.

## Configuration

Set these environment variables before `mise run run` (the service reads process environment, not `.env`):

| Variable | Required | Purpose |
|----------|----------|---------|
| `DATABASE_URL_PROFILE` | yes, every environment | Npgsql connection string (or `ConnectionStrings__ProfileDb`). No committed fallback. |
| `JWT_SECRET` | yes | HS256 secret, at least 32 bytes, shared with auth-svc. |
| `PROFILE_ENCRYPTION_KEY` | yes | Base64 of 32 random bytes (`openssl rand -base64 32`). |
| `PROFILE_ENCRYPTION_KEY_ID` | no | Id of the active key, 1-255 (default 1). |
| `PROFILE_ENCRYPTION_KEYS_RETIRED` | no | Decrypt-only keys, `id:base64key,...`. |
| `DEV_AUTH_ENABLED` | no | `true` enables `X-User-Id` auth; allowed only in Development. |

Example for local Development:

```bash
export DATABASE_URL_PROFILE='Host=localhost;Port=5432;Database=job_platform_profile;Username=postgres;Password=postgres'
export JWT_SECRET="$(openssl rand -base64 48)"   # must match the auth-svc secret for real tokens
export PROFILE_ENCRYPTION_KEY="$(openssl rand -base64 32)"
mise run run
```

## Ownership and avatars

- A profile's child rows (skills, experiences, educations) can be changed only by their owner. Another user's row and a missing row return the same `404`.
- `avatarUrl` must be an absolute `https` URL of at most 512 characters with no user information. Other values return `400`.

## Security & Encryption (SEC-08)

All sensitive PII fields (`phone`, `address`, `date_of_birth`) are encrypted using application-layer `AES-256-GCM` before persisting to PostgreSQL (`job_platform_profile`).
- Unique 12-byte Nonce per encryption.
- Master encryption key from the `PROFILE_ENCRYPTION_KEY` environment variable: base64 of 32 random bytes, used directly as the AES-256 key (`openssl rand -base64 32`). The service refuses to start if it is missing or malformed.
- Ciphertext is versioned: `Base64(KeyId[1] + Nonce[12] + Tag[16] + Ciphertext)`. Optional `PROFILE_ENCRYPTION_KEY_ID` (default `1`) names the active key, and `PROFILE_ENCRYPTION_KEYS_RETIRED` (`id:base64key,...`) holds decrypt-only keys. See `AGENTS.md` for the two-step rotation.
- Public profile API (`GET /api/profiles/{id}`) strictly excludes sensitive PII fields.

## Troubleshooting

- `dotnet: command not found` → `mise trust && mise install`
- `NU1301 local source` → `mise run pack-shared`
- `PostgreSQL connection error` → Ensure local DB is running (`cd ../job-platform-infra && docker compose up -d`) and `DATABASE_URL_PROFILE` is set in `.env`.
- `mise run verify` fails → re-run `mise run sync-env`.

`feature/* → main` (see `job-platform-docs/.github/git-strategy.md`).
