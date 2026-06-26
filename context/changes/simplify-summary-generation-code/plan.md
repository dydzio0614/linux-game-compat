# Simplify Summary Generation Code Implementation Plan

## Overview

Refactor the S-04 summary-generation path for MVP readability by flattening low-value internal seams and reducing config/default duplication. The change should make the command easier to trace from CLI entrypoint to provider call without weakening the generation safeguards that protect paid API calls, source-backed freshness, and public status correctness.

## Current State Analysis

The frame brief identifies the real problem as type-surface friction: one finite MVP command is spread across too many adjacent boundary types, while remaining config/default validation duplication adds secondary noise. The current workflow has important behavior-bearing safeguards that should remain intact.

The baseline is green: `dotnet test LinuxGameCompat.sln --no-restore` passes 142 tests before this plan is implemented.

## Desired End State

Summary generation remains behaviorally equivalent, but the code is easier to read and modify. The implementation removes one-caller/internal handoffs, keeps provider and evidence contracts where they carry real meaning, and lets configuration files own generation defaults.

### Key Discoveries

- `ICompatibilitySummaryGenerator` has one implementation and is only resolved by `Program.cs`, making the interface a low-value seam.
- `GenerateSummariesCommandOptions` is parsed and immediately copied into `SummaryGenerationRunOptions`, creating an unnecessary DTO handoff.
- `Candidate.Evidence` is populated during eligibility checks but never read later.
- `GenerationOptions` mirrors default values from `appsettings.json`, while tests assert some internal config/default shape.
- PostgreSQL tests protect real safeguards: advisory locking, failed refresh preservation, evidence-change discard, cancellation, eligibility, ordering, and AI fallback.

## What We're NOT Doing

- No database schema or migration changes.
- No UI, read-model, or public route changes.
- No provider API, prompt schema, output validation, evidence hashing, or status-mapping redesign.
- No Railway deployment, cron, or production rollout changes.
- No removal of behavior-bearing safety tests.

## Implementation Approach

Use a bounded readability refactor. Collapse local command/generator seams that only exist for internal handoff, localize generation defaults to `appsettings.json`, and update tests so they protect behavior rather than removed internal type shape.

Keep explicit contracts where they defend meaningful boundaries: provider request/result records, provider interface, prompt/evidence contracts, run result formatting, strict output validation, and persistence/concurrency safeguards.

## Phase 1: Flatten Internal Orchestration Seams

### Overview

Remove low-value command/generator handoffs while preserving the finite generation command behavior.

### Changes Required

#### 1. Generator Resolution

**File**: `LinuxGameCompat/Program.cs`, `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs`

**Intent**: Remove the one-implementation generator interface so the command path resolves the concrete scoped generator directly.

**Contract**: `CompatibilitySummaryGenerator.RunAsync(...)` remains the orchestration entrypoint. DI registers `CompatibilitySummaryGenerator` directly. Command behavior, cancellation behavior, and `SummaryGenerationRunResult` stay stable.

#### 2. Command Options Handoff

**File**: `LinuxGameCompat/Services/SummaryGeneration/GenerateSummariesCommand.cs`, `LinuxGameCompat/Program.cs`, `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs`

**Intent**: Stop parsing into one record only to immediately copy into another.

**Contract**: `GenerateSummariesCommand.TryParse` returns the same options type consumed by `CompatibilitySummaryGenerator.RunAsync`, or the parser-owned options type becomes the sole run options contract. CLI syntax remains `generate-summaries [--limit <n>] [--slug <slug>] [--force]`.

#### 3. Candidate Cleanup

**File**: `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs`

**Intent**: Remove unused candidate evidence storage and the assignment that creates it.

**Contract**: Evidence hash calculation still happens before prompt truncation for eligibility and immediately before save for stale-write protection. Removing the field must not change selected/skipped/succeeded/failed counts.

### Success Criteria

#### Automated Verification

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- CLI parser tests cover accepted options, invalid options, result formatting, and exit codes.
- PostgreSQL generator tests still cover lock contention, failed refresh preservation, evidence-change discard, cancellation, eligibility, ordering, and AI fallback.

#### Manual Verification

- Human review can trace generation from `Program.cs` to `CompatibilitySummaryGenerator.RunAsync` without adjacent option/interface indirection.

**Implementation Note**: After automated verification passes, pause for human confirmation before Phase 2.

---

## Phase 2: Localize Config Defaults and Validation

### Overview

Make configuration files the source of generation defaults while retaining validation for real safety invariants.

### Changes Required

#### 1. Generation Options Defaults

**File**: `LinuxGameCompat/Services/SummaryGeneration/GenerationOptions.cs`, `LinuxGameCompat/appsettings.json`

**Intent**: Remove duplicated default ownership between code property initializers and `appsettings.json`.

**Contract**: The configured `SummaryGeneration` section supplies defaults. Code validation still rejects invalid generation-mode settings before provider calls. Normal web startup remains able to build without `OPENAI_API_KEY`. Any tests that need valid generation settings should use one shared helper that binds `SummaryGeneration` from `LinuxGameCompat/appsettings.json`, not `new GenerationOptions()` as an implicit default source.

#### 2. Validation Scope

**File**: `LinuxGameCompat/Services/SummaryGeneration/GenerationOptions.cs`

**Intent**: Keep guardrails that prevent invalid or unsafe generation mode, while avoiding validation that exists mainly to mirror config default values.

**Contract**: Continue validating supported provider/model contract, positive limits/timeouts, one-provider-concurrency MVP behavior, and retry range. Do not duplicate exact config defaults in tests unless the value is a real contract limit.

### Success Criteria

#### Automated Verification

- Invalid generation config still returns command exit code `2`.
- Tests assert validation of real invariants without depending on duplicated default property initializers.
- Normal web startup-related tests remain green without requiring provider credentials.
- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.

#### Manual Verification

- Human review confirms `appsettings.json` is the obvious place to inspect/change generation defaults.

**Implementation Note**: After automated verification and config review pass, pause for human confirmation before Phase 3.

---

## Phase 3: Behavior-Focused Test and Documentation Cleanup

### Overview

Align tests and docs with the simplified implementation surface without losing production safeguards.

### Changes Required

#### 1. Unit Test Updates

**File**: `LinuxGameCompat.Tests/GenerateSummariesCommandTests.cs`, `LinuxGameCompat.Tests/SummaryGenerationContractTests.cs`

**Intent**: Stop tests from preserving removed internal design shape.

**Contract**: Tests should cover observable command parsing/results, provider output validation, prompt hashing/selection behavior, native status reduction, and config validation invariants. They should not require a separate command-options-to-run-options handoff or exact duplicated defaults. Replace `new GenerationOptions().Validate()` default-shape assertions with explicit invalid-invariant cases plus one config-binding test proving `SummaryGeneration` from `appsettings.json` yields valid options.

#### 2. Integration Test Preservation

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Preserve behavior-bearing safety coverage while adapting construction calls to the simplified generator contract.

**Contract**: Keep coverage for advisory-lock contention, targeted/current/hidden/no-evidence selection, failed refresh preservation, evidence changed during provider call, requested cancellation, missing/stale/failed selection, AI fallback, and oldest-attempt ordering. Generator tests should construct valid options through the shared config-bound helper after code property initializers are removed.

#### 3. Documentation Accuracy

**File**: `README.md`

**Intent**: Update command/config wording only if implementation changes make current docs inaccurate.

**Contract**: Keep documented CLI syntax, exit codes, `OPENAI_API_KEY` generation-mode requirement, and config ownership accurate.

### Success Criteria

#### Automated Verification

- Full tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.
- Release build passes: `dotnet build LinuxGameCompat.sln --configuration Release --no-restore`.

#### Manual Verification

- Fake-provider generation command exits without starting the web server.
- Normal web app startup remains unaffected by missing `OPENAI_API_KEY`.
- Human review confirms summary-generation code is easier to trace and no safety behavior was intentionally removed.

## Testing Strategy

### Unit Tests

- CLI parse success/failure, result formatting, and exit-code behavior.
- `GenerationOptions` validation for real generation invariants rather than duplicated defaults.
- Provider output validation and failure classification.
- Prompt hashing/selection behavior and native status reduction.

### Integration Tests

- Advisory-lock contention performs no provider call and exits as no-work.
- Failed refresh preserves previous successful output/status.
- Evidence changed during provider call discards stale output.
- Missing, stale, failed, current, forced, targeted, hidden, and no-evidence selection paths behave as before.
- Deterministic native status wins over AI disagreement.
- Requested cancellation propagates.

### Manual Testing Steps

1. Run the development fake-provider command and confirm it exits without starting the web server.
2. Start the normal web app without `OPENAI_API_KEY` and confirm startup remains unaffected.
3. Review the refactored summary-generation flow for fewer low-value records/interfaces and preserved safeguards.

## Performance Considerations

No intended performance changes. Provider concurrency, batch caps, prompt caps, evidence hashing, and database access patterns remain behaviorally equivalent.

## Migration Notes

No migration is required.

## References

- Frame brief: `context/changes/simplify-summary-generation-code/frame.md`
- Prior implementation plan: `context/archive/2026-06-14-generated-compatibility-synthesis/plan.md`
- Prior research: `context/archive/2026-06-14-generated-compatibility-synthesis/research.md`
- Roadmap: `context/foundation/roadmap.md` S-08
- Current orchestration: `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs`
- Current command parser: `LinuxGameCompat/Services/SummaryGeneration/GenerateSummariesCommand.cs`
- Current config: `LinuxGameCompat/Services/SummaryGeneration/GenerationOptions.cs`, `LinuxGameCompat/appsettings.json`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Flatten Internal Orchestration Seams

#### Automated

- [x] 1.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- [x] 1.2 CLI parser tests cover accepted options, invalid options, result formatting, and exit codes.
- [x] 1.3 PostgreSQL generator tests still cover lock contention, failed refresh preservation, evidence-change discard, cancellation, eligibility, ordering, and AI fallback.

#### Manual

- [x] 1.4 Human review can trace generation from `Program.cs` to `CompatibilitySummaryGenerator.RunAsync` without adjacent option/interface indirection.

### Phase 2: Localize Config Defaults and Validation

#### Automated

- [ ] 2.1 Invalid generation config still returns command exit code `2`.
- [ ] 2.2 Tests assert validation of real invariants without depending on duplicated default property initializers.
- [ ] 2.3 Normal web startup-related tests remain green without requiring provider credentials.
- [ ] 2.4 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.

#### Manual

- [ ] 2.5 Human review confirms `appsettings.json` is the obvious place to inspect/change generation defaults.

### Phase 3: Behavior-Focused Test and Documentation Cleanup

#### Automated

- [ ] 3.1 Full tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.
- [ ] 3.2 Release build passes: `dotnet build LinuxGameCompat.sln --configuration Release --no-restore`.

#### Manual

- [ ] 3.3 Fake-provider generation command exits without starting the web server.
- [ ] 3.4 Normal web app startup remains unaffected by missing `OPENAI_API_KEY`.
- [ ] 3.5 Human review confirms summary-generation code is easier to trace and no safety behavior was intentionally removed.
