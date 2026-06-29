# Generate Evidence Claims from Source Pages Implementation Plan

## Overview

Implement S-09 as one bounded operator workflow that refreshes source-backed evidence claims and then refreshes the compatibility summary for each selected game. The workflow supports ProtonDB and Are We Anti-Cheat Yet, preserves last-known-good public data on external failures, and avoids paid model calls when normalized source content and the summary are already current.

## Current State Analysis

`SourceReference` already owns source identity and citation URLs, while `EvidenceClaim` is the persisted input consumed by the existing detail page and summary generator. There is no source acquisition path, import provenance, freshness state, or stable ownership rule for generated claims. Summary generation is currently exposed as the independent `generate-summaries` command and owns batching, locking, provider calls, deterministic status reduction, and evidence rechecking.

ProtonDB's public app route is client-rendered, but its page uses a same-origin summary JSON resource. Are We Anti-Cheat Yet publishes the canonical `games.json` used to statically generate its game pages. These first-party backing resources avoid introducing a headless browser into the MVP.

## Desired End State

An operator can run `refresh-compatibility [--limit <n>] [--slug <slug>] [--force]`. For each selected visible game with a supported source, the command refreshes every supported source reference, commits changed generated claims atomically, and generates a summary when the persisted evidence changed or the summary is otherwise eligible. Visible games backed only by `Manual` claims remain summary-eligible through the same command but bypass source acquisition. A normal unchanged run fetches and validates current supported-source data but makes no OpenAI calls and performs no claim mutation.

The public read service and detail page remain unchanged: users see refreshed source-linked claims and a current summary after success, or last-known-good claims and explicitly stale summary output after a partial failure.

### Key Discoveries

- Existing claims and citations already flow to the detail page without a new read contract (`LinuxGameCompat/Services/GameCompatibilityReadService.cs:79`, `LinuxGameCompat/Components/Pages/GameDetail.razor:119`).
- The summary generator already canonicalizes all persisted claims, rechecks evidence after the provider call, and reduces source-native statuses deterministically (`LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:42`, `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:76`, `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:94`).
- The current schema has no generated-claim identity or import state, and only indexes claims by source reference (`LinuxGameCompat/Data/CompatibilityDbContext.cs:54`, `LinuxGameCompat/Data/CompatibilityDbContext.cs:68`).
- S-09's existing test strategy requires provenance, freshness, idempotency, source failure, duplicate extraction, and hostile external-data coverage (`context/foundation/test-plan.md:72`).

## What We're NOT Doing

- Adding public refresh endpoints, background scheduling, cron configuration, or fetch-on-read behavior.
- Adding headless browser infrastructure or scraping arbitrary DOM content.
- Fetching individual ProtonDB community reports through hashed internal report URLs.
- Supporting sources other than ProtonDB and Are We Anti-Cheat Yet.
- Mixing manual and generated claims on the same supported external source reference.
- Adding UI controls or a separate source-freshness warning; existing summary stale/failed messaging remains the user-facing fallback.
- Changing public game-detail routes, read models, or evidence rendering.

## Implementation Approach

Create a bounded `Services/EvidenceGeneration/` feature that constructs approved source URLs from trusted source identifiers, normalizes each source into a small versioned fact contract, hashes that semantic contract, and invokes a dedicated OpenAI provider only when the hash or extraction contract changes. Generate status claims deterministically; OpenAI may emit only caveat, workaround, and note claims.

Refresh all supported references for a game before committing any changed claims. The claim commit is a short serializable transaction; summary generation follows through a refactored per-game summary operation. Fresh claims remain committed if summary generation fails, while the previous summary is retained and marked failed/stale.

## Critical Implementation Details

### Timing and lifecycle

Never hold a database transaction during source fetches or OpenAI calls. Gather and validate all source results first, re-read the source-reference identities before claim reconciliation, and preserve the existing summary generator's post-provider evidence recheck before writing summary output.

### State sequencing

A failed source refresh records bounded attempt failure details, preserves generated claims and summary prose, marks an existing current summary stale, and does not run summary generation. Existing failed or not-generated summary states are not replaced by `Stale`. Once all source results succeed, claim changes commit first; only then may summary generation run. If a later successful unchanged refresh finds that stale summary's evidence hash and version still match the persisted claims, restore it to current without an OpenAI call; otherwise generate normally. This deliberate boundary allows fresh evidence to survive a subsequent summary-provider failure while making source-refresh uncertainty visible.

## Phase 1: Import State and Deterministic Source Adapters

### Overview

Add the persistence and safe acquisition foundation, including source-specific normalization contracts and fixture-backed parser coverage. This phase does not call OpenAI or mutate claims.

### Changes Required

#### 1. Source import persistence

**Files**: `LinuxGameCompat/Data/SourceReferenceImportState.cs`, `LinuxGameCompat/Data/SourceReference.cs`, `LinuxGameCompat/Data/CompatibilityDbContext.cs`, generated migration and model snapshot

**Intent**: Persist one operational import record per source reference without placing lifecycle state in `MetadataJson`.

**Contract**: `SourceReferenceImportState` uses `SourceReferenceId` as its primary key and cascade foreign key. It stores nullable `ContentHash` (128), `ContractVersion` (80), `LastAttemptedAt`, `LastSucceededAt`, `ETag` (512), `LastModifiedAt`, `ErrorCode` (80), and `ErrorMessage` (2000). `SourceReference` gains an optional one-to-one navigation. A row may exist before the first successful import.

#### 2. Evidence-generation configuration and transport

**Files**: `LinuxGameCompat/Services/EvidenceGeneration/EvidenceGenerationOptions.cs`, transport and source-contract files in the same feature folder, `LinuxGameCompat/appsettings.json`

**Intent**: Define bounded acquisition behavior and keep configuration ownership separate from summary generation.

**Contract**: Add an `EvidenceGeneration` section using model `gpt-5.4-mini`, maximum 10 games, 8 generated non-status claims per source, 2,500 input tokens, 800 output tokens, 15-second fetch timeout, 8 MiB decompressed response limit, 30-second provider timeout, at most two provider retries, and concurrency fixed at one. The transport accepts HTTPS JSON responses, disables automatic redirects, follows at most two explicitly revalidated redirects, and stops reading once the decompressed byte cap is exceeded. The only content-type exception is the exact AWA raw-GitHub target defined below, which may return `text/plain`; it remains subject to the same byte cap and must parse as JSON before use.

#### 3. ProtonDB adapter

**Files**: ProtonDB adapter and checked-in JSON fixtures under `LinuxGameCompat/Services/EvidenceGeneration/` and `LinuxGameCompat.Tests/Fixtures/EvidenceGeneration/`

**Intent**: Convert a ProtonDB source reference into deterministic authoritative facts without executing page JavaScript.

**Contract**: Accept only numeric `SourceGameId` values and citation URLs with host `www.protondb.com`, default HTTPS port, and path `/app/{SourceGameId}` with an optional trailing slash. Construct `https://www.protondb.com/api/v1/reports/summaries/{SourceGameId}.json` internally. Normalize effective tier (`tier`, falling back to recognized `provisionalTier` only when tier is `pending`), trending tier, best reported tier, confidence, score, and total reports. A 404, missing effective tier, or unknown tier is a source failure rather than an inferred status.

#### 4. Are We Anti-Cheat Yet adapter

**Files**: AWA adapter and checked-in JSON fixtures under the same feature/test folders

**Intent**: Read canonical source-authored anti-cheat evidence once per command and resolve individual games deterministically.

**Contract**: Accept citation URLs with host `areweanticheatyet.com`, default HTTPS port, and exact path `/game/{SourceGameId}`. Fetch `https://raw.githubusercontent.com/AreWeAntiCheatYet/AreWeAntiCheatYet/refs/heads/master/games.json` once per command, allow `application/json` or `text/plain` only for that exact target, and match the record by ordinal slug equality. Normalize status, `dateChanged`, at most eight anti-cheats, four nonblank notes, and the eight newest updates with dates/references. Treat the source `updates` array as oldest-to-newest chronology, retain its final eight entries, and preserve their source order; preserve note order and sort anti-cheats ordinally for canonicalization. Hard field limits are 120 characters per anti-cheat, 1,000 per note text or reference, 500 per update name, 120 per update date, and 1,000 per update reference; exceeding an individual field limit is a source failure. If the bounded record still exceeds the complete 2,500-token request budget, remove the oldest included update, then the last included note, then the last anti-cheat until it fits; status is never removed. Hash the final bounded fact contract actually supplied to the provider. Reject missing records, unknown statuses, and a status-only contract that cannot fit the budget.

### Success Criteria

#### Automated Verification

- Application and tests compile: `dotnet build LinuxGameCompat.sln`
- Adapter, URL, redirect, content-type, byte-limit, and normalization tests pass: `dotnet test LinuxGameCompat.sln --filter "FullyQualifiedName~EvidenceSourceAdapter|FullyQualifiedName~SourceFetch"`
- EF model matches the generated migration: `dotnet ef migrations has-pending-model-changes --project LinuxGameCompat/LinuxGameCompat.csproj`

#### Manual Verification

- Representative live ProtonDB and AWA responses still match the checked-in adapter contracts without importing data
- Generated migration and model snapshot are reviewed together for the one-to-one import-state contract

**Implementation Note**: Do not apply the migration to a persistent environment in this phase. Repository rules require all verification to pass before `database update`.

---

## Phase 2: Grounded Claim Generation and Reconciliation

### Overview

Add the bounded claim provider and the per-game evidence refresh service, including no-op detection, exclusive ownership, and last-known-good failure behavior.

### Changes Required

#### 1. Claim prompt and provider contract

**Files**: prompt builder, provider contracts, OpenAI provider, validator, and development fake under `LinuxGameCompat/Services/EvidenceGeneration/`

**Intent**: Generate useful non-status claims from normalized source facts while keeping authoritative status outside model control.

**Contract**: The provider request contains the configured model, the final bounded normalized fact contract, and output-token cap. The content hash is computed from exactly those facts so omitted overflow data neither changes claims nor creates false staleness. Strict JSON Schema returns an array of at most eight objects with `claimType` limited to `Caveat`, `Workaround`, or `Note`, plus `claimValue` and `claimText`. Instructions state that source content is untrusted data. Local validation rejects extra properties, blank or oversized fields, unknown types, count overflow, and case-insensitive duplicate `(type, value, text)` tuples. Provider output is never allowed to emit `Status`.

#### 2. Deterministic claim materialization

**Files**: evidence claim generator/reconciler in `LinuxGameCompat/Services/EvidenceGeneration/`

**Intent**: Produce a complete candidate claim set for each supported reference and avoid paid work for unchanged normalized facts.

**Contract**: Build one deterministic status claim from source name and exact native status value, then append validated provider claims. Compare the adapter content hash and adapter/prompt contract version with import state before calling OpenAI. A normal current import skips the provider. `--force` bypasses this check. Observation timestamps are assigned only to newly materialized or changed claims; exact semantic matches retain existing rows and timestamps.

#### 3. Atomic per-game reconciliation

**Files**: evidence refresh orchestration and persistence logic in `LinuxGameCompat/Services/EvidenceGeneration/`

**Intent**: Make supported external references importer-owned while preventing partial evidence replacement within a game.

**Contract**: Supported references are `ProtonDb` and `AreWeAntiCheatYet`; `Manual` references and their claims are never acquired or mutated. Fetch and generate every supported reference for a game before opening the transaction. If any source/provider step fails, preserve every existing claim for the game, update bounded attempt/failure metadata, set `IsStale = true` on an existing summary, change `Current` to `Stale` while preserving `Failed` and `NotGenerated`, and report the game failed. On success, revalidate source IDs/types/URLs and reconcile all changed source claim sets in one serializable transaction. Clear failures and update success metadata even when claims are unchanged. Set an existing summary stale when persisted semantic evidence changes. On an unchanged recovery, restore only a `Stale` summary with nonblank prose whose evidence hash and version exactly match the current canonical claims; failed, not-generated, missing, or mismatched summaries remain eligible for generation.

### Success Criteria

#### Automated Verification

- Prompt budgeting and strict provider-output contract tests pass: `dotnet test LinuxGameCompat.sln --filter "FullyQualifiedName~EvidenceClaimGenerationContract"`
- PostgreSQL import-state, idempotency, ownership, atomicity, and failure-preservation tests pass: `dotnet test LinuxGameCompat.sln --filter "FullyQualifiedName~PostgreSqlEvidenceGeneration"`
- Existing summary-generation contract tests remain green: `dotnet test LinuxGameCompat.sln --filter "FullyQualifiedName~SummaryGenerationContract"`

#### Manual Verification

- Fixture-driven output demonstrates deterministic status claims and source-grounded non-status claims without unsupported advice
- Failure records are bounded and contain no raw page bodies, prompts, credentials, or provider response payloads

**Implementation Note**: Pause after automated verification so claim wording and failure sanitization can be reviewed before command integration.

---

## Phase 3: Unified Compatibility Refresh Command

### Overview

Replace the independent summary command with one operator workflow, reuse the established summary invariants per game, and finish integration, documentation, and migration verification.

### Changes Required

#### 1. Per-game summary operation

**Files**: `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs` and narrowly related summary-generation contracts

**Intent**: Let the compatibility refresh orchestrator synthesize one game after evidence reconciliation without duplicating summary logic.

**Contract**: Extract a per-game summary operation that accepts a game identifier and force intent, returns generated/skipped/failed outcome plus token counts, and preserves current prompt selection, deterministic status reduction, evidence recheck, short serializable write, last-good prose, and sanitized failure state. Batch selection and advisory locking move out of summary generation.

#### 2. Refresh command and orchestration

**Files**: refresh command/orchestrator under `LinuxGameCompat/Services/EvidenceGeneration/`, `LinuxGameCompat/Program.cs`

**Intent**: Enforce evidence and summary refresh through one finite command.

**Contract**: Recognize only `refresh-compatibility [--limit <1..10>] [--slug <slug>] [--force]`; remove `generate-summaries` dispatch. Select visible games that either contain a supported reference or contain only `Manual` claims and have a missing/stale/failed summary (or are forced). Supported-source games derive their work age from the oldest `LastAttemptedAt` across supported references and sort as never attempted when any supported reference has no attempt; manual-only games derive it from summary `LastAttemptedAt` and sort as never attempted when absent. Combine both cohorts under one limit, ordered by never attempted, work age, then game ID, and process sequentially. One dedicated PostgreSQL advisory lock covers the full run. Refresh every supported reference; manual-only games bypass acquisition. After successful acquisition, generate a summary only when claims changed, the summary is missing/stale/failed, or force is set. Before generation, an unchanged refresh may instead restore a matching stale summary to current without calling the provider. If evidence fails, skip summary generation. If summary generation fails after claims commit, retain fresh claims and old prose while leaving the summary failed/stale.

#### 3. Operator result, development mode, and documentation

**Files**: command result contracts/tests, `LinuxGameCompat/Program.cs`, `README.md`, configuration helpers

**Intent**: Make the combined workflow measurable and locally testable without accidental production bypasses.

**Contract**: Aggregate selected, succeeded, failed, skipped, changed-claim games, generated summaries, duration, input/output tokens, and lock contention. Exit `0` for success/no work/contention, `1` for item failures, `2` for invalid arguments/configuration, and `130` for cancellation. In Development only, `COMPATIBILITY_REFRESH_USE_FAKE_PROVIDERS=true` substitutes both evidence and summary providers. Normal web startup requires neither provider credentials nor evidence-generation validation; refresh mode requires valid configuration and `OPENAI_API_KEY` unless fake providers are active.

### Success Criteria

#### Automated Verification

- Command parsing, selection, no-op, force, aggregation, exit-code, lock, and cancellation tests pass: `dotnet test LinuxGameCompat.sln --filter "FullyQualifiedName~RefreshCompatibility"`
- PostgreSQL end-to-end tests cover changed claims followed by summary success/failure and concurrent evidence rechecks: `dotnet test LinuxGameCompat.sln --filter "FullyQualifiedName~PostgreSqlCompatibilityRefresh"`
- Full solution verification passes: `dotnet test LinuxGameCompat.sln`
- Release build succeeds: `dotnet build LinuxGameCompat.sln --configuration Release`
- EF model has no uncommitted schema drift: `dotnet ef migrations has-pending-model-changes --project LinuxGameCompat/LinuxGameCompat.csproj`

#### Manual Verification

- After all automated checks, reviewed migration applies cleanly: `dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj`
- A development fake-provider run succeeds, and its immediate unchanged rerun reports no claim or summary provider work
- A bounded live `--slug` run produces source-linked claims and a current summary, and a subsequent unchanged run makes no OpenAI calls

**Implementation Note**: Persistent or production migration application and live provider spend require the normal operator approval and backup precautions.

---

## Testing Strategy

### Unit Tests

- Source identifier, citation URL, exact fetch target, redirect, source-specific content-type, and response-size validation.
- ProtonDB effective-tier and AWA slug/status/fact normalization from checked-in fixtures, including field failures, collection caps, newest-update retention, deterministic budget reduction, and stable hashes.
- Canonical fact hashing across ordering and transport metadata changes.
- Hostile source content, prompt budgeting, strict structured output, deduplication, and length/count bounds.
- Command parsing, output formatting, exit codes, and configuration validation.

### Integration Tests

- One-to-one import-state schema, cascade behavior, and migration compatibility.
- Visible/supported eligibility, combined supported/manual-only work-age ordering, limit/slug behavior, and manual-source acquisition exclusion.
- Unchanged no-op with stable claim IDs/timestamps and zero provider calls.
- Atomic multi-reference replacement, duplicate prevention, summary staleness, and deterministic public status after synthesis.
- Last-good preservation for fetch, parse, unknown-status, evidence-provider, and summary-provider failures, including stale summary output and unchanged recovery without model work.
- Refresh lock contention and summary evidence-change race handling.

### Manual Testing Steps

1. Compare live source response shapes with fixtures without persisting data.
2. Run the combined command with development fake providers for one seeded slug.
3. Rerun unchanged and confirm provider counters and changed/generated counts remain zero.
4. Run against live providers for one approved slug and inspect claims, citations, import state, summary state, and public detail output.
5. Simulate a source failure and confirm claims and prose remain while the summary becomes stale; recover with unchanged evidence and confirm the summary returns to current without a model call.
6. Simulate a summary failure after evidence change and confirm fresh claims remain while old prose carries the stale warning.

## Performance Considerations

- Process games and provider calls sequentially for the MVP.
- Fetch and parse AWA's global dataset once per command, then resolve all selected slugs from an in-memory ordinal dictionary.
- Stream responses through the decompressed byte cap rather than buffering unbounded content.
- Use normalized content hashes and contract versions to avoid unnecessary evidence and summary provider calls.
- Keep all database transactions short and outside network activity.

## Migration Notes

The migration is additive: it introduces the import-state table and relationship without rewriting existing claims. Existing claims on supported external references remain public until the first successful import, after which the importer owns and reconciles them. Application rollback can ignore the additive table, but rolling back data behavior does not restore generated claims that replaced prior seeded claims. Review and back up persistent data before the first live import.

## References

- Research: `context/changes/generate-evidence-claims-from-source-pages/research.md`
- Roadmap outcome: `context/foundation/roadmap.md:200`
- Test risks: `context/foundation/test-plan.md:72`
- Existing summary invariants: `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:22`
- Existing provider pattern: `LinuxGameCompat/Services/SummaryGeneration/OpenAiCompatibilitySummaryProvider.cs:20`
- AWA canonical data repository: `https://github.com/AreWeAntiCheatYet/AreWeAntiCheatYet`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Import State and Deterministic Source Adapters

#### Automated

- [x] 1.1 Application and tests compile — ba88b79
- [x] 1.2 Adapter, URL, redirect, content-type, byte-limit, and normalization tests pass — ba88b79
- [x] 1.3 EF model matches the generated migration — ba88b79

#### Manual

- [x] 1.4 Representative live ProtonDB and AWA responses still match the checked-in adapter contracts without importing data — ba88b79
- [x] 1.5 Generated migration and model snapshot are reviewed together for the one-to-one import-state contract — ba88b79

### Phase 2: Grounded Claim Generation and Reconciliation

#### Automated

- [x] 2.1 Prompt budgeting and strict provider-output contract tests pass
- [x] 2.2 PostgreSQL import-state, idempotency, ownership, atomicity, and failure-preservation tests pass
- [x] 2.3 Existing summary-generation contract tests remain green

#### Manual

- [x] 2.4 Fixture-driven output demonstrates deterministic status claims and source-grounded non-status claims without unsupported advice
- [x] 2.5 Failure records are bounded and contain no raw page bodies, prompts, credentials, or provider response payloads

### Phase 3: Unified Compatibility Refresh Command

#### Automated

- [ ] 3.1 Command parsing, selection, no-op, force, aggregation, exit-code, lock, and cancellation tests pass
- [ ] 3.2 PostgreSQL end-to-end tests cover changed claims followed by summary success/failure and concurrent evidence rechecks
- [ ] 3.3 Full solution verification passes
- [ ] 3.4 Release build succeeds
- [ ] 3.5 EF model has no uncommitted schema drift

#### Manual

- [ ] 3.6 After all automated checks, reviewed migration applies cleanly
- [ ] 3.7 A development fake-provider run succeeds, and its immediate unchanged rerun reports no claim or summary provider work
- [ ] 3.8 A bounded live `--slug` run produces source-linked claims and a current summary, and a subsequent unchanged run makes no OpenAI calls
