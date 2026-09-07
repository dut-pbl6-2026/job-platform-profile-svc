# job-platform-profile-svc

Candidate & User Profile microservice for **Vietnam Job Platform** (`pbl6`) — `dut-pbl6-2026`. `Profile CRUD` `Skills` `WorkExperience` `Education` `AES-256-GCM (SEC-08)`, `Port 5005` `net10.0`.

- Tech: .NET 10 Web API (`net10.0`, C# 13, Clean Architecture)
- Branch flow: `feature/* → main` (see `job-platform-docs/.github/git-strategy.md`)
- Jira: Epic `PBL6-3` (Profile & Candidate Domain, 5 pts, Day Tue W3)
- TM: TM1 Hoai, TM2 Thanh (Owner), TM3 Chi Bao, TM4 Khoa

## Deploy (Render Free jp-profile — TM4 Khoa)

- Service: `jp-profile` `https://jp-profile.onrender.com` `5005`
- Hook: `RENDER_DEPLOY_HOOK_PROFILE`

## Overview

- `src/Profile.Api` — Web API Minimal APIs, `Swagger`, `/health`, `auto-migrate`, `DevAuthMiddleware`
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

## Run

```bash
mise run run              # dotnet run --project src/Profile.Api
curl http://localhost:5005/health   # {"status":"ok","service":"profile"}
```

Sample requests:

### Upsert profile (Candidate)
```bash
curl -X PUT http://localhost:5005/api/profiles/me \
  -H "Content-Type: application/json" \
  -H "X-User-Id: a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d" \
  -H "X-User-Role: Candidate" \
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
  -H "X-User-Id: a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"
```

### View public profile (PII phone/address/DOB masked/excluded)
```bash
curl http://localhost:5005/api/profiles/a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d
```

### Add skill
```bash
curl -X POST http://localhost:5005/api/profiles/skills \
  -H "Content-Type: application/json" \
  -H "X-User-Id: a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d" \
  -d '{"name": "C# / .NET", "proficiency": 5, "yearsOfExperience": 4}'
```

### Add work experience
```bash
curl -X POST http://localhost:5005/api/profiles/experiences \
  -H "Content-Type: application/json" \
  -H "X-User-Id: a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d" \
  -d '{
    "companyName": "Tech Corp",
    "position": "Software Engineer",
    "startDate": "2022-01-01",
    "isCurrent": true,
    "description": "Leading microservice migration"
  }'
```

## Security & Encryption (SEC-08)

All sensitive PII fields (`phone`, `address`, `date_of_birth`) are encrypted using application-layer `AES-256-GCM` before persisting to PostgreSQL (`job_platform_profile`).
- Unique 12-byte Nonce per encryption.
- Storage format: `Base64(Nonce[12B] + Tag[16B] + Ciphertext[NB])`.
- Master encryption key derived from `PROFILE_ENCRYPTION_KEY` environment variable.
- Public profile API (`GET /api/profiles/{id}`) strictly excludes sensitive PII fields.

## Troubleshooting

- `dotnet: command not found` → `mise trust && mise install`
- `NU1301 local source` → `mise run pack-shared`
- `PostgreSQL connection error` → Ensure local DB is running (`cd ../job-platform-infra && docker compose up -d`) and `DATABASE_URL_PROFILE` is set in `.env`.
- `mise run verify` fails → re-run `mise run sync-env`.

`feature/* → main` (see `job-platform-docs/.github/git-strategy.md`).
