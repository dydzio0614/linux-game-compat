<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Member Favorites Tracking

- **Plan**: `context/changes/member-favorites-tracking/plan.md`
- **Scope**: Phases 1-3 of 3
- **Date**: 2026-06-14
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical 2 warnings 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Concurrent favorite add can report failure

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Adherence / Safety & Quality
- **Location**: LinuxGameCompat/Services/MemberFavoritesService.cs:70
- **Detail**: The plan requires add/remove to be idempotent so double-clicks, retries, and multiple tabs converge. `AddCurrentMemberFavoriteAsync` prechecks existence, but if two requests race, one insert can win and the other catches the unique-index `DbUpdateException` as `Failed`.
- **Fix**: Treat unique `(MemberId, GameId)` conflicts as success, or use PostgreSQL `INSERT ... ON CONFLICT DO NOTHING`.
  - Strength: Matches the plan’s idempotency requirement at the DB race boundary.
  - Tradeoff: Needs provider-specific handling or raw SQL.
  - Confidence: HIGH — the unique index is present and this race follows directly from the current precheck/insert flow.
  - Blind spot: I did not run a synthetic concurrent-add repro.
- **Decision**: FIXED — Applied atomic PostgreSQL `INSERT ... ON CONFLICT DO NOTHING` and added duplicate-add regression coverage.

### F2 — Concurrent favorite remove can bubble an exception

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: LinuxGameCompat/Services/MemberFavoritesService.cs:104
- **Detail**: `RemoveCurrentMemberFavoriteAsync` loads a favorite and deletes it. If another request deletes the same row between load and save, EF can raise a concurrency/update exception instead of treating the final “already absent” state as success.
- **Fix**: Replace load-then-remove with filtered `ExecuteDeleteAsync` after the visible-game check, treating 0 or 1 affected rows as success.
  - Strength: Makes remove atomic and naturally idempotent.
  - Tradeoff: Requires EF Core bulk-delete path instead of tracked deletion.
  - Confidence: MEDIUM — likely under concurrent same-row deletion; worth covering with a test.
  - Blind spot: No concurrent-remove repro was executed.
- **Decision**: FIXED — Replaced load-then-remove with filtered `ExecuteDeleteAsync`, treating already-absent rows as success.
