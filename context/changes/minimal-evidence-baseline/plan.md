# Minimal Evidence Baseline Implementation Plan

## Overview

Establish the backend foundation for source-backed Linux game compatibility evidence. This change adds a PostgreSQL-backed persistence model, local development database setup, curated baseline data, read services, and verification coverage so later roadmap slices can build search/detail UI and GPT-generated summaries on top of a durable contract.

## Current State Analysis

The app is currently a .NET 10 Blazor Web App starter with no app-level database integration, no migrations, no domain model, no source ingestion model, and no test project. Railway deployment already exists, including a production Postgres service, but the application does not consume the database yet.

The PRD and roadmap make this slice foundational: it unlocks anonymous compatibility lookup and browseable game lists while avoiding broad crawling, a complete game database, public UI, auth, favorites, and full AI summary generation.

## Desired End State

The repository has a Postgres-backed evidence baseline that can store games, source references, claim-level evidence, optional future-generated summaries, and hidden/manual-control state. Developers can run a local PostgreSQL database, apply explicit EF Core migrations, seed a small representative catalog, and verify the persistence/read-service behavior with automated tests.

### Key Discoveries

- `context/foundation/roadmap.md` defines `minimal-evidence-baseline` as foundation `F-01`, unlocking `S-01` and `S-03`.
- `context/foundation/prd.md` requires source links for compatibility status, caveat, and workaround claims.
- `context/foundation/shape-notes.md` names ProtonDB and AreWeAntiCheatYet as initial source-service candidates.
- The current app builds successfully but has no DB package, schema, migrations, or data-layer code.
- Railway status confirms project `linux-game-compat`, production web service, and production Postgres are online.
- ProtonDB community API is not a dependable runtime dependency for this slice because the hosted deployment was terminated and self-hosting adds MongoDB/stale-backup complexity.

## What We're NOT Doing

- No public search, browse, or game detail UI.
- No GPT mini call path, prompt construction, background summary generation, retries, cost controls, or observability.
- No broad crawling, complete game database, automated refresh scheduler, or community-thread browsing.
- No auth, member favorites, account model, or admin UI.
- No dependency on `protondb-community-api` as a runtime service.
- No automatic production migrations during app startup.

## Implementation Approach

Add a conservative EF Core/Npgsql persistence foundation around a small, deterministic domain model. Use local Docker Compose PostgreSQL for reproducible development, Railway Postgres through environment configuration for production, and explicit migration commands for schema changes. Store source-backed evidence at claim granularity so downstream UI and GPT summary work can preserve traceability.

## Phase 1: Persistence Foundation

### Overview

Introduce PostgreSQL persistence infrastructure without adding product-facing behavior.

### Changes Required

#### 1. Database Dependencies And Tooling

**File**: `LinuxGameCompat/LinuxGameCompat.csproj`

**Intent**: Add the packages needed for EF Core with PostgreSQL and design-time migration support.

**Contract**: Reference Npgsql EF Core provider, EF Core design tooling, and any required test-time package decisions in the relevant test project created later.

#### 2. Local PostgreSQL Setup

**File**: `docker-compose.yml`

**Intent**: Provide a reproducible local PostgreSQL database for development and integration verification.

**Contract**: Define a local-only Postgres service with stable database/user/password values intended for development only.

#### 3. Connection Configuration

**File**: `LinuxGameCompat/appsettings.Development.json`

**Intent**: Add local development connection settings while keeping production secrets out of the repository.

**Contract**: Development config points at local Postgres; production uses Railway-provided environment variables such as `DATABASE_URL`.

#### 4. DbContext Registration

**File**: `LinuxGameCompat/Program.cs`

**Intent**: Register the compatibility DbContext and connection-string resolution.

**Contract**: Resolve local configuration in development and Railway-style `DATABASE_URL` in production. Do not apply migrations on startup.

#### 5. Initial Migration

**File**: `LinuxGameCompat/Migrations/*`

**Intent**: Create the initial database schema through EF Core migrations.

**Contract**: Migrations are explicit and committed; applying them is a separate command such as `dotnet ef database update`.

### Success Criteria

#### Automated Verification

- Project builds: `dotnet build LinuxGameCompat.sln --no-restore`
- EF tooling can create/apply the initial migration against local Postgres.
- App startup does not attempt automatic migration.

#### Manual Verification

- Developer can start local Postgres and apply migrations manually.
- Railway production Postgres remains configured externally; no secrets are committed.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Evidence Domain, Summaries, And Seed Data

### Overview

Define the durable domain model and seed a small representative baseline.

### Changes Required

#### 1. Game And Source Entities

**File**: `LinuxGameCompat/Data/*`

**Intent**: Add core persistence entities for games, source systems, and external references.

**Contract**: `Game` includes title, optional Steam App ID, slug, normalized status, `IsHidden`, and timestamps. Source/reference entities preserve source type, URL, and source-specific identifiers.

#### 2. Evidence Claims

**File**: `LinuxGameCompat/Data/*`

**Intent**: Store compatibility evidence at claim granularity so each status, caveat, workaround, or note can be traced to a source.

**Contract**: `EvidenceClaim` supports claim type, claim text/value, source URL, source metadata, and relation to a game/source. Claims require source metadata; games do not require claims.

#### 3. Summary-Ready Schema

**File**: `LinuxGameCompat/Data/*`

**Intent**: Reserve database space for future GPT mini-generated compatibility summaries without implementing generation.

**Contract**: `GameCompatibilitySummary` stores summary text, summary status/state, provider/model metadata, evidence version/hash, generated timestamp, stale/error state, and relation to a game. Missing summary data is valid.

#### 4. DbContext Mapping

**File**: `LinuxGameCompat/Data/CompatibilityDbContext.cs`

**Intent**: Configure entity relationships, required fields, indexes, uniqueness, and query-friendly constraints.

**Contract**: Slugs are unique; source references avoid duplicate source/game identifiers; hidden games remain stored; no-source games are valid with `Unknown` status.

#### 5. Curated Seed Data

**File**: `LinuxGameCompat/Data/Seed/*`

**Intent**: Provide a deterministic 5-10 game baseline that supports downstream search/detail development.

**Contract**: Include at least one no-evidence game, one hidden game, AreWeAntiCheatYet-backed claims, ProtonDB reference links, and optional summary-shaped placeholder data for tests.

### Success Criteria

#### Automated Verification

- Schema migration includes all evidence, source, game, hidden, and summary-ready fields.
- Seed process loads representative records without requiring external network calls.
- No-source games pass validation and default to `Unknown`.

#### Manual Verification

- Local database contains visible games, hidden games, claim-level source links, and optional summary records after seeding.
- Hidden records are present in storage for manual control but identifiable as hidden.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Read Services And Validation

### Overview

Expose app-facing backend services for downstream lookup UI and enforce the foundation's data integrity rules.

### Changes Required

#### 1. Read Models And Service Interfaces

**File**: `LinuxGameCompat/Services/*`

**Intent**: Provide read contracts for visible games, game details, source references, evidence claims, and optional stored summaries.

**Contract**: Normal read paths exclude hidden games by default and return no-source games with `Unknown` status and no claims.

#### 2. Validation Rules

**File**: `LinuxGameCompat/Services/*`

**Intent**: Enforce source-link integrity for evidence claims while allowing catalog entries that do not yet have evidence.

**Contract**: Evidence claims require source metadata; games require a title and slug; hidden state is independent of validity.

#### 3. Status Normalization

**File**: `LinuxGameCompat/Data/*`

**Intent**: Provide a minimal internal compatibility status contract for downstream code without locking public UI copy.

**Contract**: Use a source-neutral internal enum such as `Unknown`, `Unsupported`, `PlayableWithCaveats`, and `Playable`; final labels remain out of scope for this change.

### Success Criteria

#### Automated Verification

- Read services return visible games and exclude hidden games by default.
- Read services return evidence claims and optional summaries for a selected visible game.
- Validation rejects source-backed claims without source metadata.
- Validation accepts games that have no evidence claims.

#### Manual Verification

- A developer can inspect service/query output for visible, hidden, no-source, source-backed, and summary-shaped records.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Verification And Handoff

### Overview

Add the automated safety net and developer-facing instructions needed to maintain the foundation.

### Changes Required

#### 1. Test Project

**File**: `LinuxGameCompat.Tests/*`

**Intent**: Add focused tests for mapping, validation, seed data, read services, and summary-ready schema behavior.

**Contract**: Unit tests cover deterministic domain rules; integration tests run against PostgreSQL using Testcontainers or the local Compose database.

#### 2. Documentation

**File**: `README.md` or `context/changes/minimal-evidence-baseline/implementation-notes.md`

**Intent**: Document local DB startup, migration commands, seed behavior, Railway production assumptions, and the future GPT integration boundary.

**Contract**: Include explicit migration commands and state that production migrations are not automatic.

#### 3. Verification Commands

**File**: repository scripts or documentation only

**Intent**: Make the expected verification path clear for future agents and humans.

**Contract**: Verification includes build, migration apply, unit tests, and PostgreSQL integration tests.

### Success Criteria

#### Automated Verification

- Full solution builds: `dotnet build LinuxGameCompat.sln --no-restore`
- Unit tests pass.
- PostgreSQL integration tests pass.
- Migration applies cleanly to a local PostgreSQL database.

#### Manual Verification

- Developer can follow documentation from a clean checkout to start local Postgres, apply migrations, seed data, and inspect baseline records.
- Future GPT summary work has a documented schema boundary but no implemented GPT dependency in F-01.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to plan closeout.

---

## Testing Strategy

### Unit Tests

- Slug generation and uniqueness expectations.
- Source claim validation.
- No-source game validity.
- Hidden-game behavior.
- Minimal status normalization.
- Optional summary behavior and stale/error state modeling.

### Integration Tests

- EF Core migration applies to PostgreSQL.
- Seed data loads successfully.
- Visible game query excludes hidden records.
- Game detail query returns source references, claims, and optional summaries.
- Evidence claims preserve source URLs and metadata.

### Manual Testing Steps

1. Start local PostgreSQL using Docker Compose.
2. Apply migrations explicitly.
3. Load curated seed data.
4. Inspect visible, hidden, no-source, source-backed, and summary-shaped records.
5. Confirm no secrets are committed and Railway DB configuration remains external.

## Performance Considerations

The baseline targets a small MVP catalog. Add indexes for slug lookup, Steam App ID lookup, source reference lookup, and visible-game listing. Avoid broad import/refresh performance work until the source ingestion roadmap requires it.

## Migration Notes

Use explicit EF Core migrations. Do not apply migrations automatically on application startup, especially in Railway production. Production migration execution should remain a separate deploy/runbook step.

## References

- PRD: `context/foundation/prd.md`
- Roadmap: `context/foundation/roadmap.md`
- Shape notes: `context/foundation/shape-notes.md`
- Infrastructure: `context/foundation/infrastructure.md`
- AreWeAntiCheatYet repository: `https://github.com/AreWeAntiCheatYet/AreWeAntiCheatYet`
- ProtonDB community API research source: `https://github.com/Trsnaqe/protondb-community-api`
- Npgsql EF Core provider: `https://www.npgsql.org/efcore/`
- Testcontainers PostgreSQL module: `https://dotnet.testcontainers.org/modules/postgres/`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Persistence Foundation

#### Automated

- [ ] 1.1 Project builds
- [ ] 1.2 EF migration can be applied against local Postgres
- [ ] 1.3 App startup does not auto-apply migrations

#### Manual

- [ ] 1.4 Developer can start local Postgres and apply migrations manually
- [ ] 1.5 Railway secrets remain external and uncommitted

### Phase 2: Evidence Domain, Summaries, And Seed Data

#### Automated

- [ ] 2.1 Migration includes evidence, source, game, hidden, and summary-ready fields
- [ ] 2.2 Seed loads representative records without external network calls
- [ ] 2.3 No-source games validate with Unknown status

#### Manual

- [ ] 2.4 Local database contains visible, hidden, claim-level evidence, and optional summary records
- [ ] 2.5 Hidden records are present but identifiable as hidden

### Phase 3: Read Services And Validation

#### Automated

- [ ] 3.1 Read services return visible games and exclude hidden games by default
- [ ] 3.2 Read services return evidence claims and optional summaries for visible games
- [ ] 3.3 Validation rejects claims without source metadata
- [ ] 3.4 Validation accepts games without evidence claims

#### Manual

- [ ] 3.5 Developer can inspect service output for visible, hidden, no-source, source-backed, and summary-shaped records

### Phase 4: Verification And Handoff

#### Automated

- [ ] 4.1 Full solution builds
- [ ] 4.2 Unit tests pass
- [ ] 4.3 PostgreSQL integration tests pass
- [ ] 4.4 Migration applies cleanly to local PostgreSQL

#### Manual

- [ ] 4.5 Documentation supports clean local setup, migration, seeding, and record inspection
- [ ] 4.6 Future GPT summary boundary is documented without implementing GPT dependency
