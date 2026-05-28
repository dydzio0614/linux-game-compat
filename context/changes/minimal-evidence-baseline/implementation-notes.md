# Minimal Evidence Baseline Implementation Notes

These notes document the local verification and handoff path for F-01. They are change-specific runbook notes, not public project README copy.

## Local Database

Start the development database:

```bash
docker compose up -d postgres
```

The local connection string is stored in `LinuxGameCompat/appsettings.Development.json` and points to:

```text
Host=localhost;Port=5433;Database=linux_game_compat;Username=linux_game_compat;Password=linux_game_compat_dev
```

Apply migrations explicitly:

```bash
dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj
```

EF seed data is embedded in the migrations/model through `LinuxGameCompat/Data/Seed/CompatibilitySeedData.cs`. Applying migrations loads the representative baseline without external network calls.

Inspect baseline records with `psql` if available:

```bash
PGPASSWORD=linux_game_compat_dev psql -h localhost -p 5433 -U linux_game_compat -d linux_game_compat
```

Useful checks inside `psql`:

```sql
select "Title", "Slug", "CompatibilityStatus", "IsHidden" from "Games" order by "Title";
select g."Title", ss."Name", sr."Url" from "SourceReferences" sr join "Games" g on g."Id" = sr."GameId" join "SourceSystems" ss on ss."Id" = sr."SourceSystemId" order by g."Title";
select g."Title", ec."ClaimType", ec."ClaimValue", ec."ClaimText" from "EvidenceClaims" ec join "Games" g on g."Id" = ec."GameId" order by g."Title";
select g."Title", s."State", s."SummaryStatus", s."Provider", s."Model", s."IsStale" from "GameCompatibilitySummaries" s join "Games" g on g."Id" = s."GameId" order by g."Title";
```

## Verification

Build the full solution:

```bash
dotnet build LinuxGameCompat.sln --no-restore
```

Run unit and PostgreSQL integration tests:

```bash
dotnet test LinuxGameCompat.sln --no-restore
```

The integration tests use Testcontainers and require Docker access. They start an isolated PostgreSQL container, apply EF Core migrations, verify seed data, and exercise the read service.

To verify migrations against the local Compose database:

```bash
docker compose up -d postgres
dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj
```

## Production Database Assumptions

Production uses Railway-provided database configuration through `DATABASE_URL`. Secrets must stay outside the repository.

The app does not run migrations on startup. Production schema changes must be applied as an explicit deployment/runbook step, not as application boot behavior.

## Future GPT Summary Boundary

`GameCompatibilitySummary` is a storage boundary for future generated compatibility summaries. It records lifecycle state, summary status, summary text, provider/model metadata, evidence version/hash, generated timestamp, stale marker, and error fields.

F-01 intentionally does not call GPT models, build prompts, run background jobs, or introduce summary generation retries/cost controls. Later GPT work should consume source-backed `EvidenceClaim` and `SourceReference` rows, write summary results into `GameCompatibilitySummary`, and preserve the evidence version/hash used for each generated result.
