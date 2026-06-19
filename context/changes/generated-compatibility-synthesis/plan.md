# Generated Compatibility Synthesis Implementation Plan

## Overview

Add a finite `generate-summaries` process mode that converts curated evidence into cached OpenAI-generated prose while deriving the public compatibility status deterministically from source-native statuses. The existing web process only reads stored results; generation is bounded, concurrency-safe, and manually operated on Railway before any schedule is enabled.

## Current State Analysis

The app already stores games, source references, evidence claims, and one optional `GameCompatibilitySummary` per game. The read service maps summary metadata, and the detail page renders any non-empty summary text without considering lifecycle state or staleness.

`Program.cs` always starts the web host. There is no provider integration, evidence canonicalization, native-status parser, generation orchestration, advisory locking, retry policy, or finite CLI mode. Railway currently runs the web service and PostgreSQL only.

The baseline is healthy: `dotnet build LinuxGameCompat.sln --no-restore` succeeds with zero warnings and all 71 tests pass.

## Desired End State

An operator can run `generate-summaries` as a bounded process. Eligible games receive source-grounded generated prose, while their public status is reduced deterministically across ProtonDB and Are We Anti-Cheat Yet evidence. AI status is used only when native evidence provides no recognized status and remains visible as an advisory assessment when it disagrees with the deterministic result.

Public pages distinguish current, stale, failed, unavailable, fallback, and disagreement states without exposing provider errors. Raw source evidence remains visible and authoritative.

### Key Discoveries

- `GameCompatibilitySummary` already carries the core lifecycle, provenance, freshness, and error fields needed for generated output (`LinuxGameCompat/Data/GameCompatibilitySummary.cs`).
- The current detail page ignores summary state and staleness (`LinuxGameCompat/Components/Pages/GameDetail.razor`).
- The Docker entrypoint is web-specific, but Railway can override it with a complete Start Command for a separate finite service (`Dockerfile`).
- Railway cron jobs require terminating processes; the first rollout will remain manual until runtime and token use are measured.
- ProtonDB publishes legacy medals and newer tier labels; Are We Anti-Cheat Yet defines `Broken`, `Running`, `Denied`, `Supported`, and `Planned`.

## What We're NOT Doing

- No source scraping, evidence ingestion, broad crawling, or automated evidence refresh.
- No persistent worker, queue, cron schedule, or public on-demand provider calls.
- No generation-run or per-attempt ledger table.
- No individually cited generated sentences; existing evidence claims and links remain the citation surface.
- No personalized hardware, distribution, settings, or library analysis.
- No component-test framework or live OpenAI calls in automated tests.

## Implementation Approach

Use one artifact with a default web mode and a finite `generate-summaries` mode. Introduce a narrow provider interface backed by the official OpenAI .NET package and test orchestration through a fake provider.

Canonicalize and hash the complete evidence set before prompt truncation. Parse source-native status claims through explicit source-specific maps, reduce recognized results pessimistically, and call the model for concise prose plus an advisory/fallback status. Coordinate whole runs with a PostgreSQL session advisory lock and recheck evidence immediately before committing output.

## Critical Implementation Details

### Status authority

`Game.CompatibilityStatus` is the public authority. Use deterministic source reduction whenever at least one native status maps successfully; otherwise use the AI status as fallback. `GameCompatibilitySummary.SummaryStatus` always records the AI assessment. Never let an optimistic AI result override a more pessimistic deterministic status.

### Freshness and concurrency

Hash every evidence claim and its source identity before applying the 12-claim/2,500-token prompt limits. Hold a dedicated session-level advisory lock for the run, but do not hold an EF transaction or row lock across a provider request. Re-read and re-hash evidence before saving; discard output if it changed.

### Native status mapping

- ProtonDB `Platinum`, `Native`, `Gold`, `T1`, `S`, `A`, `Playable`, and `Verified` map to `Playable`.
- ProtonDB `Bronze`, `Silver`, `T2`-`T4`, and `B`-`D` map to `PlayableWithCaveats`.
- ProtonDB `Borked`, `T5`, `F`, and `Unsupported` map to `Unsupported`.
- ProtonDB pending, unknown, `?`, and unrecognized values provide no deterministic signal.
- Are We Anti-Cheat Yet `Supported` maps to `Playable`, `Running` to `PlayableWithCaveats`, and `Broken`, `Denied`, and `Planned` to `Unsupported`.
- Reduce recognized statuses as `Unsupported > PlayableWithCaveats > Playable`. `Planned` is unsupported because the product describes current practical compatibility, not future prognosis.

## Phase 1: Native Evidence Normalization and Generation Contracts

### Overview

Define deterministic inputs, provider contracts, prompt limits, and OpenAI output validation independently of persistence orchestration.

### Changes Required

#### 1. Native Status Parser and Reducer

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Convert source-native status claim values into the application's four-status vocabulary and derive the most pessimistic recognized status across all sources.

**Contract**: Parsing is keyed by `SourceSystemType`, trims input, compares case-insensitively, implements the mapping above, and returns no signal for unknown values. Reduction ignores no-signal values and returns no deterministic result when none map.

#### 2. Canonical Evidence and Freshness

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Make freshness independent of EF collection ordering and prompt truncation.

**Contract**: Canonical input includes source type/name/identity/URL plus claim ID/type/value/text/observation time in stable ordinal order. Store an uppercase SHA-256 hex hash and a generator contract version. Prompt selection occurs only after hashing.

#### 3. Prompt Selection and Token Budget

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Keep every provider request within the accepted cost envelope while retaining a useful evidence mix.

**Contract**: Select at most 12 claims: four status, three caveat, three workaround, and two note claims, newest first with stable ID tie-breaking. Enforce 2,500 input tokens using the selected model's tokenizer and 500 maximum output tokens. Fail before the provider call if the prompt cannot satisfy the input limit.

#### 4. Provider Interface and OpenAI Adapter

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Isolate external API behavior and make generation testable without network calls.

**Contract**: Add `ICompatibilitySummaryProvider.GenerateAsync(request, cancellationToken)`. The production adapter uses the official `OpenAI` package and Responses API with model `gpt-5.4-mini`, supplied through configuration. Requests use reasoning effort `none`, low text verbosity, strict JSON-schema output, and `store: false`. Structured output contains exactly a normalized status and non-empty plain-text summary no longer than 4,000 characters. Treat an incomplete response, including exhaustion of the 500-token output budget, as permanent malformed output for that attempt.

Re-verify `gpt-5.4-mini` against current official OpenAI documentation during implementation; fail configuration validation rather than silently substituting another model.

#### 5. Generation Configuration

**File**: `LinuxGameCompat/appsettings.json`

**Intent**: Centralize bounded generation defaults without requiring provider secrets during web startup.

**Contract**: Defaults are provider `OpenAI`, model `gpt-5.4-mini`, maximum 10 games, 12 claims, 2,500 input tokens, 500 output tokens, concurrency one, 30-second request timeout, and two SDK retries (three total HTTP attempts). Read the secret from `OPENAI_API_KEY` and validate it only in generation mode.

### Success Criteria

#### Automated Verification

- Every listed ProtonDB and Are We Anti-Cheat Yet status maps exactly as specified.
- ProtonDB Gold maps to `Playable`.
- Unknown native values provide no deterministic signal.
- Pessimistic reduction is independent of claim order.
- Canonical hashes are order-independent and change when any source or claim field changes.
- Prompt truncation never changes the complete evidence hash and never exceeds configured limits.
- Structured provider output rejects unknown statuses, blank text, and oversized text.
- Retry classification distinguishes transient, permanent, and cancelled requests.
- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual Verification

- Human confirms generation contracts before persistence work.

**Implementation Note**: After automated verification passes, pause for human confirmation before Phase 2.

---

## Phase 2: Safe Orchestration and CLI Mode

### Overview

Add summary attempt metadata, concurrency-safe state transitions, bounded selection, and a finite command path that never starts Kestrel.

### Changes Required

#### 1. Summary Attempt Metadata and Migration

**File**: `LinuxGameCompat/Data/GameCompatibilitySummary.cs`, `LinuxGameCompat/Data/CompatibilityDbContext.cs`, `LinuxGameCompat/Migrations/`

**Intent**: Support fair retry ordering and retain the latest provider usage without creating a separate telemetry model.

**Contract**: Add nullable `LastAttemptedAt`, `InputTokenCount`, and `OutputTokenCount` fields with an explicit forward-only migration and updated model snapshot. Existing summaries remain valid.

#### 2. Generation Orchestrator

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Select, generate, and persist summaries with bounded cost and consistent lifecycle behavior.

**Contract**: `ICompatibilitySummaryGenerator.RunAsync(options, cancellationToken)` processes visible games with at least one evidence claim. Eligible records are missing, failed, stale, or differ by full evidence hash/generator version; `--force` additionally admits current records. Order null/oldest `LastAttemptedAt` first, then game ID.

Before limiting provider work, mark all detected hash mismatches stale. On success, persist current prose, AI status, provenance, hash/version, generated/attempt timestamps, usage, and cleared errors; update `Game.CompatibilityStatus` from deterministic status or AI fallback. On failure, preserve prior successful prose/provenance and the last public `Game.CompatibilityStatus`, set `Failed` and `IsStale`, and store sanitized bounded errors. This intentionally retains a potentially stale status until generation succeeds; detail-page trust labels communicate summary staleness, while list and favorites status remain last-known. Requested cancellation is propagated.

#### 3. Concurrency and Evidence Recheck

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Prevent duplicate paid calls and stale writes across cron, manual, or accidental concurrent execution.

**Contract**: Acquire one stable PostgreSQL session advisory lock with `pg_try_advisory_lock`. Lock contention emits the final no-work result and exits successfully. Release in `finally`. Re-hash evidence after the provider response and discard changed-input output as stale.

#### 4. Timeout and Retry Policy

**File**: `LinuxGameCompat/Services/SummaryGeneration/`

**Intent**: Recover from ordinary provider instability without unbounded time or spend.

**Contract**: Configure the official OpenAI SDK as the only retry layer, with at most two retries (three total HTTP attempts) for its supported transient failures. Do not add an application-level retry loop. Use a 30-second timeout per HTTP attempt, propagate requested cancellation, and surface the terminal SDK failure through the adapter's transient/permanent classification. Authentication, local validation, and malformed or incomplete output are permanent failures and are never retried by application code.

#### 5. Command Dispatch

**File**: `LinuxGameCompat/Program.cs`

**Intent**: Preserve default web startup while exposing a finite, testable generation mode.

**Contract**: Support `generate-summaries [--limit 1..10] [--slug <slug>] [--force]`. `--slug` narrows selection but still skips current output unless paired with `--force`. Exit codes: `0` success/no work/lock contention, `1` one or more attempted games failed, `2` invalid arguments or configuration, and `130` cancellation. Output one aggregate result line containing selected/succeeded/failed/skipped counts, duration, and token totals.

### Success Criteria

#### Automated Verification

- Migration applies cleanly to a fresh PostgreSQL database and preserves seeded summaries.
- Missing, stale, failed, current, forced, targeted, hidden, and no-evidence selection paths behave as specified.
- Concurrent generator execution performs no duplicate provider calls.
- Evidence changed during generation cannot receive stale output.
- Failed refresh preserves prior successful text and hides bounded operator errors from public consumers.
- Deterministic status wins disagreement; AI is used only when native parsing returns no signal.
- Oldest-attempted ordering prevents repeated failures from starving later games.
- CLI parser, configuration validation, and every exit code are covered.
- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual Verification

- Running the command locally with a fake/test provider exits without starting the web server.
- Normal web startup remains unchanged and does not require `OPENAI_API_KEY`.

**Implementation Note**: After automated verification and manual command checks pass, pause for human confirmation before Phase 3.

---

## Phase 3: Trust-Aware Summary UI

### Overview

Present generated output without obscuring deterministic status, source evidence, freshness, or provider failure.

### Changes Required

#### 1. Public Summary Read Contract

**File**: `LinuxGameCompat/Services/GameReadModels.cs`, `LinuxGameCompat/Services/GameCompatibilityReadService.cs`

**Intent**: Give the UI the lifecycle and authority information it needs without leaking operator-only errors.

**Contract**: Retain state, AI summary status, text, generated timestamp, and staleness. Remove `ErrorCode` and `ErrorMessage` from the public summary read model. Expose whether deterministic and AI statuses disagree and whether AI supplied the public fallback.

#### 2. Lifecycle-Aware Detail Rendering

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`

**Intent**: Make generated content useful while communicating its freshness and authority accurately.

**Contract**: Current output shows generated prose and date. Stale or failed output with preserved prose remains visible with an outdated warning. Failed output without prior prose shows temporary unavailability. Missing output remains silent. Provider error details are never rendered.

When AI disagrees, retain deterministic status as primary and show the AI assessment as advisory. When no native status maps, disclose that the displayed status is a generated fallback. Keep grouped raw claims and links immediately below the generated section.

#### 3. Summary Styling

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Distinguish generated, stale, failed, disagreement, and fallback messaging without redesigning the page.

**Contract**: Reuse existing status and evidence styles; add accessible warning/advisory treatments and preserve narrow-screen behavior.

### Success Criteria

#### Automated Verification

- Public read models contain no provider error code or message.
- Read-service tests cover current, stale, failed, disagreement, and AI-fallback metadata.
- Existing visible/hidden/no-evidence lookup and favorites tests remain green.
- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual Verification

- Current summaries show prose and generation date.
- Stale and failed summaries preserve useful prose with an explicit warning.
- Provider failures never expose internal errors publicly.
- Deterministic/AI disagreement and AI fallback are clearly disclosed.
- Raw evidence and source links remain visible and authoritative.
- Summary states remain readable on narrow mobile widths.

**Implementation Note**: After automated verification and UI review pass, pause for human confirmation before Phase 4.

---

## Phase 4: Manual Railway Rollout and Handoff

### Overview

Deploy the finite process separately, validate real usage, and defer scheduling until measured data justifies it.

### Changes Required

#### 1. Operator Documentation

**File**: `README.md` and change implementation notes

**Intent**: Document local/production configuration, command behavior, migration order, safe reruns, and failure recovery.

**Contract**: Include `OPENAI_API_KEY`, generation options, exit codes, no-op semantics, explicit migration procedure, error inspection, and the rule that raw evidence remains authoritative.

#### 2. Railway Generation Service

**Intent**: Run generation independently from public traffic using the existing image and PostgreSQL database.

**Contract**: Create a separate Railway service using Start Command `dotnet LinuxGameCompat.dll generate-summaries --limit 10`, the shared `DATABASE_URL`, scoped `OPENAI_API_KEY`, and restart policy `Never`. Do not attach a cron schedule in this change.

#### 3. Representative Production Run

**Intent**: Validate model quality, runtime, usage, persistence, and rendering before scheduling is considered.

**Contract**: After human approval and migration, trigger one manual bounded run. Record duration, input/output token totals, outcomes, and observed Railway resource usage in implementation notes. Re-run unchanged input to prove idempotent no-work behavior.

### Success Criteria

#### Automated Verification

- Release build passes: `dotnet build LinuxGameCompat.sln --configuration Release --no-restore`.
- Full tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.
- Published image retains unchanged default web startup.

#### Manual Verification

- Human approves production migration and provider spend before the first run.
- Railway generation service terminates after its bounded batch and is not restarted.
- Re-running unchanged evidence makes no provider calls.
- Web service remains online and does not require provider credentials.
- Production pages show deterministic status, generated prose, trust labels, and unchanged raw source evidence.
- Measured runtime/token/resource data is captured before any cron schedule is proposed.

**Implementation Note**: Scheduling is a separate follow-up decision after the measured handoff is reviewed.

## Testing Strategy

### Unit Tests

- Exhaustive native status mappings, including ProtonDB Gold as `Playable` and AWA Planned as `Unsupported`.
- Pessimistic reduction, unknown inputs, AI fallback, and disagreement.
- Evidence canonicalization/hash stability and prompt selection/token limits.
- Structured-output validation, retry classification, cancellation, and CLI parsing.

### Integration Tests

- EF migration and summary metadata compatibility.
- PostgreSQL advisory locking, eligibility, fairness, lifecycle transitions, idempotency, and evidence-change recheck.
- Success, failed initial generation, failed refresh with preserved text, targeted execution, and force behavior through a fake provider.

### Manual Testing Steps

1. Run generation locally against representative evidence and inspect stored status/prose.
2. Verify every public lifecycle, fallback, and disagreement state.
3. Apply the production migration after approval and run one Railway batch.
4. Re-run unchanged evidence and confirm zero provider calls.
5. Confirm the web service and source-linked evidence path remain healthy.

## Performance Considerations

Provider concurrency remains one. Each run is capped at 10 games, each prompt at 12 claims/2,500 input tokens, and each response at 500 output tokens. Full evidence hashing may scan more claims than the prompt includes, but the MVP data volume is small and correctness takes priority over premature caching.

## Migration Notes

Add only nullable summary-attempt metadata. Apply the migration explicitly before deploying/running the new command. App rollback does not roll back PostgreSQL; the additive nullable migration remains compatible with the previous web image.

## References

- Research: `context/changes/generated-compatibility-synthesis/research.md`
- Roadmap: `context/foundation/roadmap.md` S-04
- Product requirements: `context/foundation/prd.md` US-01, FR-004, and non-functional requirements
- Existing summary schema: `LinuxGameCompat/Data/GameCompatibilitySummary.cs`
- Existing read/UI path: `LinuxGameCompat/Services/GameCompatibilityReadService.cs`, `LinuxGameCompat/Components/Pages/GameDetail.razor`
- Are We Anti-Cheat Yet status contract: `https://github.com/AreWeAntiCheatYet/AreWeAntiCheatYet/blob/master/src/types/games.ts`
- ProtonDB rating definitions: `https://github.com/bdefore/protondb-i18n/blob/master/locales/en-US/protondb-content.json`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Native Evidence Normalization and Generation Contracts

#### Automated

- [x] 1.1 Every listed ProtonDB and Are We Anti-Cheat Yet status maps exactly as specified. — ee84d98
- [x] 1.2 ProtonDB Gold maps to `Playable`. — ee84d98
- [x] 1.3 Unknown native values provide no deterministic signal. — ee84d98
- [x] 1.4 Pessimistic reduction is independent of claim order. — ee84d98
- [x] 1.5 Canonical hashes are order-independent and change when any source or claim field changes. — ee84d98
- [x] 1.6 Prompt truncation never changes the complete evidence hash and never exceeds configured limits. — ee84d98
- [x] 1.7 Structured provider output rejects unknown statuses, blank text, and oversized text. — ee84d98
- [x] 1.8 Retry classification distinguishes transient, permanent, and cancelled requests. — ee84d98
- [x] 1.9 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`. — ee84d98
- [x] 1.10 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`. — ee84d98

#### Manual

- [x] 1.11 Human confirms generation contracts before persistence work. — ee84d98

### Phase 2: Safe Orchestration and CLI Mode

#### Automated

- [x] 2.1 Migration applies cleanly to a fresh PostgreSQL database and preserves seeded summaries. — e152630
- [x] 2.2 Missing, stale, failed, current, forced, targeted, hidden, and no-evidence selection paths behave as specified. — e152630
- [x] 2.3 Concurrent generator execution performs no duplicate provider calls. — e152630
- [x] 2.4 Evidence changed during generation cannot receive stale output. — e152630
- [x] 2.5 Failed refresh preserves prior successful text and hides bounded operator errors from public consumers. — e152630
- [x] 2.6 Deterministic status wins disagreement; AI is used only when native parsing returns no signal. — e152630
- [x] 2.7 Oldest-attempted ordering prevents repeated failures from starving later games. — e152630
- [x] 2.8 CLI parser, configuration validation, and every exit code are covered. — e152630
- [x] 2.9 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`. — e152630
- [x] 2.10 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`. — e152630

#### Manual

- [x] 2.11 Running the command locally with a fake/test provider exits without starting the web server. — e152630
- [x] 2.12 Normal web startup remains unchanged and does not require `OPENAI_API_KEY`. — e152630

### Phase 3: Trust-Aware Summary UI

#### Automated

- [x] 3.1 Public read models contain no provider error code or message.
- [x] 3.2 Read-service tests cover current, stale, failed, disagreement, and AI-fallback metadata.
- [x] 3.3 Existing visible/hidden/no-evidence lookup and favorites tests remain green.
- [x] 3.4 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- [x] 3.5 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual

- [x] 3.6 Current summaries show prose and generation date.
- [x] 3.7 Stale and failed summaries preserve useful prose with an explicit warning.
- [x] 3.8 Provider failures never expose internal errors publicly.
- [x] 3.9 Deterministic/AI disagreement and AI fallback are clearly disclosed.
- [x] 3.10 Raw evidence and source links remain visible and authoritative.
- [x] 3.11 Summary states remain readable on narrow mobile widths.

### Phase 4: Manual Railway Rollout and Handoff

#### Automated

- [ ] 4.1 Release build passes: `dotnet build LinuxGameCompat.sln --configuration Release --no-restore`.
- [ ] 4.2 Full tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.
- [ ] 4.3 Published image retains unchanged default web startup.

#### Manual

- [ ] 4.4 Human approves production migration and provider spend before the first run.
- [ ] 4.5 Railway generation service terminates after its bounded batch and is not restarted.
- [ ] 4.6 Re-running unchanged evidence makes no provider calls.
- [ ] 4.7 Web service remains online and does not require provider credentials.
- [ ] 4.8 Production pages show deterministic status, generated prose, trust labels, and unchanged raw source evidence.
- [ ] 4.9 Measured runtime/token/resource data is captured before any cron schedule is proposed.
