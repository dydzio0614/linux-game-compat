# LinuxGameCompat

LinuxGameCompat is an early-stage web app for checking Linux game compatibility from source-backed evidence. The goal is to help people switching to Linux quickly understand whether a game is likely to work, what caveats exist, and which external sources support those claims.

Primary reason of existence of this app is practicing AI-native software development workflow with heavy use of proprietary coding agent skills.

The project currently contains a .NET Blazor Server app backed by PostgreSQL. It includes anonymous game search and browse flows, game detail pages with source-linked evidence, passwordless member access, member favorites with current compatibility status, a compatibility evidence data model, seed data, Entity Framework Core migrations, and tests around compatibility data validation and PostgreSQL integration.

## Features

- Search curated games by title.
- Show compact normalized compatibility statuses in search results.
- Store source-backed compatibility evidence from systems such as ProtonDB and Are We Anti-Cheat Yet?
- Track source references, evidence claims, and generated compatibility summaries.
- Hide internal or suppressed game records from public lookup.
- Let signed-in members save visible games as favorites and view a personal favorites list with current compatibility status.

Planned MVP work includes broader source coverage and compatibility-summary refresh behavior.

## Tech Stack

- .NET 10
- Blazor Server / Razor Components
- Entity Framework Core
- PostgreSQL
- xUnit
- Testcontainers for PostgreSQL integration tests
- Docker for containerized deployment

## Repository Layout

```text
LinuxGameCompat/          Web application source
LinuxGameCompat.Tests/    Automated tests
context/                  Product, planning, and implementation notes
Dockerfile                Production container build
docker-compose.yml        Local PostgreSQL dependency
```

## Prerequisites

- .NET 10 SDK
- Docker or another local PostgreSQL 18-compatible database

For local development, the included Docker Compose file starts PostgreSQL on `127.0.0.1:5433` with the development credentials already referenced by `LinuxGameCompat/appsettings.Development.json`.

## Getting Started

Start PostgreSQL:

```bash
docker compose up -d
```

Restore dependencies:

```bash
dotnet restore LinuxGameCompat.sln
```

Apply database migrations:

```bash
dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj
```

Run the app:

```bash
dotnet run --project LinuxGameCompat/LinuxGameCompat.csproj
```

Then open the local URL printed by `dotnet run`.

## Configuration

The app reads the database connection from either:

- `ConnectionStrings:CompatibilityDatabase`
- `DATABASE_URL`

`DATABASE_URL` is useful for hosted environments. The app converts it to an Npgsql connection string and enables required SSL mode.

Compatibility refresh additionally reads `OPENAI_API_KEY`. The key is validated only in refresh mode; normal web startup does not require provider credentials. Evidence and summary generation defaults are configured under `EvidenceGeneration` and `SummaryGeneration` in `LinuxGameCompat/appsettings.json`.

Passwordless login normally sends magic links by email. For trusted local, test, or demo deployments that cannot use SMTP, set `Auth:ShowMagicLinksInFrontend=true` to show the generated bearer login link in the `/login` success panel. Keep this disabled for normal public production traffic: anyone who can see or copy that displayed link can sign in as the requested email address until the one-use link expires.

## Compatibility Refresh

Generate a bounded batch without starting the web server:

```bash
OPENAI_API_KEY="..." dotnet run --project LinuxGameCompat/LinuxGameCompat.csproj -- refresh-compatibility --limit 10
```

Supported options:

- `--limit <1..10>` limits the selected games (default: 10).
- `--slug <slug>` targets one game.
- `--force` bypasses evidence and summary freshness checks; combine it with `--slug` for a deliberate targeted refresh.

The command refreshes supported source evidence before synthesizing summaries and prints aggregate selection, outcome, changed-claim, generated-summary, duration, token, and lock-contention metrics. Exit codes are `0` for success, no work, or advisory-lock contention; `1` for item failures; `2` for invalid arguments or configuration; and `130` for cancellation. An unchanged rerun fetches source facts but performs no provider work or claim mutation.

In Development only, set `COMPATIBILITY_REFRESH_USE_FAKE_PROVIDERS=true` to exercise both generation stages without `OPENAI_API_KEY`:

```bash
COMPATIBILITY_REFRESH_USE_FAKE_PROVIDERS=true dotnet run --project LinuxGameCompat/LinuxGameCompat.csproj -- refresh-compatibility --slug baldurs-gate-3
```

Raw, source-linked compatibility evidence remains authoritative. Generated prose is a bounded synthesis; deterministic source status takes precedence, and the detail page labels stale output, AI fallback, or disagreement.

### Database migration and recovery

Apply migrations explicitly before deploying or running a new generator image:

```bash
dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj
```

For Railway, run the equivalent command against the production `DATABASE_URL` only after reviewing the pending migration and taking the normal database backup/rollback precautions. The summary-attempt migration is additive and nullable; application rollback does not reverse the database migration.

On a failed run, inspect the aggregate command output and the operator-only `GameCompatibilitySummaries.ErrorCode` and `ErrorMessage` columns. Provider errors are sanitized and bounded and are never exposed by the public read model. Fix credentials, configuration, connectivity, or source data as indicated, then rerun the same bounded command. Failed refreshes preserve the last successful prose and public status while marking it stale.

## Tests

Run the test suite with:

```bash
dotnet test LinuxGameCompat.sln
```

Some tests use Testcontainers and require Docker to be available.

## Deployment

The repository includes a multi-stage Dockerfile:

```bash
docker build -t linux-game-compat .
docker run -p 8080:8080 -e DATABASE_URL="postgres://user:password@host:5432/dbname" linux-game-compat
```

The container listens on `PORT` when provided, otherwise it defaults to `8080`.

The artifact is compatible with a future separate Railway generation service built from the same repository and Dockerfile. That follow-up service should share PostgreSQL `DATABASE_URL`, scope `OPENAI_API_KEY` to itself, and use Start Command:

```text
dotnet LinuxGameCompat.dll refresh-compatibility --limit 10
```

Its restart policy should be `Never`. Production service creation, migration, provider spend, and measured rollout are intentionally deferred. Do not attach a cron schedule until a representative manual run and an unchanged-input no-work rerun have been reviewed. Keep the existing web service's start command and credentials unchanged.
