<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Generate Evidence Claims from Source Pages

- **Plan**: `context/changes/generate-evidence-claims-from-source-pages/plan.md`
- **Scope**: Phases 1–3 of 3
- **Date**: 2026-06-29
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 7 warnings, 3 observations
- **Triage**: COMPLETE — 7 fixed, 3 skipped

## Triage Summary

- **Fixed**: F1, F2, F3, F6, F7, F8, F10
- **Skipped**: F4, F5, F9
- **Verification**: 185 tests passed; Release build succeeded with 0 warnings; EF model has no pending changes

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | FAIL |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

## Findings

### F1 — Generated claims cross an inadequately framed prompt boundary

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/SummaryGeneration/EvidencePromptBuilder.cs:77`
- **Detail**: External source text can become generated claim text and is then interpolated into a line-oriented summary prompt. The first provider explicitly treats source text as untrusted; the summary provider only says “Use only supplied evidence.” Newlines and instruction-shaped text are not strongly separated from summary instructions.
- **Fix**: Treat every summary evidence field as untrusted inert data in the system instructions, serialize selected claims as a JSON data envelope, and add hostile/newline claim tests.
  - Strength: Closes the new source→claim→summary trust boundary.
  - Tradeoff: Changes the summary prompt contract/version and may regenerate otherwise-current summaries.
  - Confidence: HIGH — the current formatter directly concatenates fields.
  - Blind spot: Provider behavior still requires a bounded live evaluation.
- **Decision**: FIXED — summary evidence is JSON-framed as inert data under contract v2, with hostile-input coverage

### F2 — The 15-second fetch timeout does not bound the streamed body

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/EvidenceGeneration/SourceFetchTransport.cs:26`
- **Detail**: HttpClient uses `ResponseHeadersRead`, then reads the response stream with only the caller token. `HttpClient.Timeout` does not bound the subsequent streamed read or the complete redirect chain, so a slow source can exceed the planned 15-second acquisition bound.
- **Fix**: Use one linked cancellation source with `CancelAfter` for the complete fetch, including redirects and body reads, and test a stalled body.
- **Decision**: FIXED — one linked deadline now bounds redirects and streamed body reads, with `fetch_timeout` coverage

### F3 — Source-identity drift bypasses failure-state handling

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/EvidenceGeneration/EvidenceRefreshService.cs:68`
- **Detail**: If a supported reference changes between acquisition and reconciliation, the method returns `source_identity_changed` without recording bounded failure metadata or marking the existing summary stale. Old claims can remain attached to the changed reference while the public summary remains Current.
- **Fix**: After ending the reconciliation transaction, record the identity failure against current supported references and stale the summary through the standard failure path.
  - Strength: Restores the plan’s last-known-good/staleness invariant.
  - Tradeoff: Requires restructuring the early-return transaction path.
  - Confidence: HIGH — the current branch returns before `RecordFailureAsync`.
  - Blind spot: Concurrent deletion needs a missing-reference-safe recorder.
- **Decision**: FIXED — identity drift now records failure against locked current references and stales retained summary data

### F4 — A ten-game run loads every visible game and claim first

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/EvidenceGeneration/CompatibilityRefreshOrchestrator.cs:88`
- **Detail**: Candidate selection executes `ToListAsync` with summary, references, import state, and all evidence claims before ordering and applying `Take(limit)` in memory. The operator limit bounds provider work but not database rows or memory.
- **Fix**: Project eligibility and work age in SQL, order and take candidate IDs server-side, and return only the primitive fields needed by the processing loop.
  - Strength: Makes the configured limit an actual resource bound.
  - Tradeoff: The combined supported/manual query needs careful nullable-age tests.
  - Confidence: HIGH — `Take` occurs after materialization.
  - Blind spot: Current production dataset size was not measured.
- **Decision**: SKIPPED — accepted for the current MVP dataset scale

### F5 — Planned command and recovery coverage is missing

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Adherence
- **Location**: `LinuxGameCompat.Tests/RefreshCompatibilityTests.cs:7`
- **Detail**: The exact 3.1 filter runs six parser/result unit tests; it does not match `PostgreSqlCompatibilityRefreshTests` because the words are reversed. No tests cover combined supported/manual ordering, oldest-reference age, limit ordering, manual acquisition exclusion, unchanged stale-summary restoration, Failed/NotGenerated preservation, or importer ownership.
- **Fix**: Rename or filter the integration class so 3.1 executes it and add focused PostgreSQL cases for the omitted selection, ownership, and recovery contracts.
  - Strength: Makes the checked success criterion exercise what it names.
  - Tradeoff: Adds container-backed test setup and runtime.
  - Confidence: HIGH — the executed filter reported only six tests.
  - Blind spot: Some old summary-race coverage exists under another class and passes in the full suite.
- **Decision**: SKIPPED — existing full-suite and integration coverage accepted without broadening this remediation

### F6 — Failed runs underreport known provider token usage

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `LinuxGameCompat/Services/EvidenceGeneration/EvidenceRefreshService.cs:55`
- **Detail**: A successful paid claim call followed by a later-reference failure returns zero tokens. A successful summary call discarded by the evidence recheck also returns a Failed result with zero tokens. Aggregate cost metrics are lower than actual known usage.
- **Fix**: Accumulate usage after each provider response and carry it through partial-failure and evidence-changed result paths, with regression tests for both cases.
- **Decision**: FIXED — known provider usage is retained across partial-failure and evidence-race paths

### F7 — AWA reduction uses a different token formula than the provider

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `LinuxGameCompat/Services/EvidenceGeneration/AreWeAntiCheatYetSourceAdapter.cs:71`
- **Detail**: AWA trims using `facts tokens + 512`; the actual prompt builder uses facts, instructions, schema, and a 64-token protocol reserve. Independent formulas can discard facts early or allow a contract that the real builder rejects, contrary to the complete-request 2,500-token contract.
- **Fix**: Share one exact request-token calculator between AWA reduction and `EvidenceClaimPromptBuilder`, preserving the planned removal order.
- **Decision**: FIXED — AWA reduction and provider construction now share the exact request-token calculation

### F8 — Recovery documentation points operators at incomplete state

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `README.md:120`
- **Detail**: README still calls the migration “summary-attempt,” tells operators to inspect only `GameCompatibilitySummaries` errors, and omits the first-live-import claim-replacement backup warning. Source errors actually live on `SourceReferenceImportStates`.
- **Fix**: Correct the migration/recovery section and include import-state error inspection plus the first-live-import backup warning.
- **Decision**: FIXED — recovery guidance now covers import state, migration identity, and first-live-import backup risk

### F9 — Manual completion has no reviewable evidence

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: `context/changes/generate-evidence-claims-from-source-pages/plan.md:270`
- **Detail**: Manual items 1.4–3.8 are checked with commit SHAs, but the diff contains no verification log for live source shapes, migration application, fake unchanged rerun, bounded live run, or payload-sanitization inspection.
- **Fix**: Preserve a verification log for the completed checks, or reopen any check whose result cannot be reproduced.
- **Decision**: SKIPPED — existing checked progress and commit references accepted without a separate verification log

### F10 — Whitespace-only stale prose can be restored as Current

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/EvidenceGeneration/EvidenceRefreshService.cs:100`
- **Detail**: Restoration checks `SummaryText.Length > 0`, while the plan requires nonblank prose and the UI treats whitespace as unavailable.
- **Fix**: Gate restoration with `!string.IsNullOrWhiteSpace(summary.SummaryText)`.
- **Decision**: FIXED — stale-summary restoration now requires nonblank prose, with PostgreSQL regression coverage

## Automated Verification

| Check | Result |
|-------|--------|
| Debug build | PASS — 0 warnings, 0 errors |
| Adapter/fetch filter | PASS — 25 tests |
| Evidence claim contract filter | PASS — 4 tests |
| PostgreSQL evidence filter | PASS — 3 tests |
| Summary contract filter | PASS — 49 tests |
| RefreshCompatibility filter | PASS — 6 tests |
| PostgreSQL compatibility refresh filter | PASS — 5 tests |
| Full solution | PASS — 180 tests |
| Release build | PASS — 0 warnings, 0 errors |
| EF model drift | PASS — no pending model changes |
