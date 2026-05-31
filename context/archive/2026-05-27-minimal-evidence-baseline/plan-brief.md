# Minimal Evidence Baseline - Plan Brief

> Full plan: `context/changes/minimal-evidence-baseline/plan.md`

## What & Why

This change establishes the backend data foundation for the Linux game compatibility app. It creates a source-backed PostgreSQL evidence model so later lookup UI and GPT-generated summaries can rely on durable, traceable game compatibility data.

## Starting Point

The current app is a .NET 10 Blazor starter with Railway deployment and Railway Postgres provisioned, but no app-level persistence, domain model, migrations, seed data, services, or tests.

## Desired End State

Developers can run local Postgres, apply explicit EF Core migrations, load a small curated catalog, and query visible games with source-backed evidence and optional summary-ready records. Games without evidence are valid and hidden records can be manually controlled.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Slice scope | Backend foundation only | Keeps F-01 focused on the data path that unlocks later lookup UI. | Plan |
| Database | PostgreSQL via EF Core/Npgsql | Matches existing Railway Postgres and avoids a later persistence migration. | Plan |
| Local DB | Docker Compose Postgres | Gives agents and humans a reproducible local setup. | Plan |
| Migrations | Explicit CLI/runbook only | Avoids startup-time production schema mutation. | Plan |
| ProtonDB | Reference-only in F-01 | The community API is not a reliable runtime dependency for this baseline. | Plan |
| Evidence shape | Claim-level evidence | Supports PRD traceability for status, caveats, and workarounds. | PRD / Plan |
| Empty evidence | Valid game with `Unknown` status | Allows catalog entries before evidence exists and enables later UI warning. | Plan |
| Hidden records | `IsHidden` on games | Allows manual suppression of broken or unwanted records without deletion. | Plan |
| LLM summaries | Schema only | Reserves space for GPT mini summaries without adding AI integration scope. | Plan |

## Scope

**In scope:**

- EF Core/Npgsql persistence setup.
- Local PostgreSQL Docker Compose setup.
- Explicit migration workflow.
- Game, source, external reference, evidence claim, and summary-ready schema.
- Curated 5-10 game seed dataset.
- Hidden-game control and no-source game support.
- Read services and validation.
- Unit and PostgreSQL integration tests.
- Local/production DB documentation.

**Out of scope:**

- Public search/detail/browse UI.
- GPT mini API calls, prompts, background jobs, retries, and cost controls.
- Broad crawling, complete catalog import, or automated refresh.
- Auth, favorites, admin UI, and account features.
- Runtime dependency on `protondb-community-api`.

## Architecture / Approach

Use the existing Blazor/ASP.NET Core app as the host for EF Core persistence and backend read services. PostgreSQL is the durable store; curated seed data creates deterministic baseline records; read services expose visible games, evidence claims, source references, and optional stored summaries to later UI slices.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Persistence Foundation | EF/Npgsql, local Postgres, config, migration workflow | Connection-string handling between local and Railway |
| 2. Evidence Domain, Summaries, And Seed Data | Durable schema and curated records | Over-modeling before lookup UI exists |
| 3. Read Services And Validation | App-facing query contracts and integrity rules | Hidden/no-source behavior leaking into downstream UI unexpectedly |
| 4. Verification And Handoff | Tests and developer setup docs | Integration tests depending on local Docker/Postgres availability |

**Prerequisites:** Railway Postgres exists; Docker is available for the default local development path.

**Estimated effort:** About 2-3 focused implementation sessions across 4 phases.

## Open Risks & Assumptions

- Final public compatibility status labels remain unresolved and belong to a later UI slice.
- GPT summary generation will be implemented later, but the schema must preserve provider/model provenance and stale/error state.
- AreWeAntiCheatYet can inform curated seed data; broad automated source import is not part of F-01.
- Production migrations need an explicit runbook before real data exists.

## Success Criteria (Summary)

- Local Postgres can be started, migrated, seeded, and queried.
- Source-backed claims preserve links/metadata, while games without claims remain valid as `Unknown`.
- Hidden records stay stored but are excluded from normal read services.
- Summary-ready schema exists without introducing GPT mini runtime dependency.
