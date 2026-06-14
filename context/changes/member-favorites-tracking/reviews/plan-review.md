<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Member Favorites Tracking Implementation Plan

- **Plan**: `context/changes/member-favorites-tracking/plan.md`
- **Mode**: Deep
- **Date**: 2026-06-14
- **Verdict**: SOUND with accepted implementation risks
- **Findings**: 1 critical 2 warnings 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | WARNING |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | WARNING |
| Plan Completeness | PASS |

## Grounding

8/8 existing paths verified, 5 new-path additions expected, 10/10 symbols verified, brief-to-plan consistency verified.

## Findings

### F1 — Phase 1 requires tests that Phase 3 adds later

- **Severity**: ❌ CRITICAL
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1 success criteria / Phase 3 test phase / Progress
- **Detail**: Phase 1 required schema, owner-isolation, hidden-game, idempotency, status, and ordering checks before the Phase 1 pause, but the original test implementation work was planned in Phase 3.
- **Fix**: Move the favorites PostgreSQL integration tests and required AuthTestHarness support into Phase 1. Keep Phase 3 as final regression/manual smoke/documentation only.
- **Decision**: FIXED — moved PostgreSQL integration tests and AuthTestHarness support into Phase 1; Phase 3 now covers documentation and final verification.

### F2 — Detail page controls need InteractiveServer render mode

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: End-State Alignment
- **Location**: Phase 2 — Game Detail Favorite Controls
- **Detail**: Phase 2 requires inline add/remove controls on `GameDetail.razor`, but only the new `/favorites` page is explicitly told to use `@rendermode InteractiveServer`. Existing interactive pages declare the render mode.
- **Fix**: Add `@rendermode InteractiveServer` to the Phase 2 contract for `LinuxGameCompat/Components/Pages/GameDetail.razor`.
- **Decision**: ACCEPTED — leave render mode implicit for implementation.

### F3 — Idempotent add needs explicit unique-race handling

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Implementation Approach / Phase 1 service contract
- **Detail**: The plan promises double-click, retry, and multiple-tab idempotency, but the service contract only requires duplicate add success if the row already exists. Two concurrent add attempts can still race on the unique `(MemberId, GameId)` constraint.
- **Fix ⭐ Recommended**: Specify that `AddCurrentMemberFavoriteAsync` must treat the database unique-constraint race as success, and add an automated PostgreSQL concurrency test.
  - Strength: Matches the multiple-tab/retry promise and keeps the unique index as the source of truth.
  - Tradeoff: Requires provider-aware exception handling or a raw SQL conflict-tolerant insert path.
  - Confidence: HIGH — current EF service patterns use direct `SaveChangesAsync`; unique constraints are already tested by causing `DbUpdateException`.
  - Blind spot: Exact provider error-code helper has not been selected.
- **Decision**: ACCEPTED — keep duplicate-add expectations to sequential idempotency only.
