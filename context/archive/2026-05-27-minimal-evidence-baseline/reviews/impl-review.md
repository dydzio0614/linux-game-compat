<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Minimal Evidence Baseline

- **Plan**: `context/changes/minimal-evidence-baseline/plan.md`
- **Scope**: Phases 1-4 of 4
- **Date**: 2026-05-29
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 1 observation

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

### F1 - Source-backed citation integrity is underconstrained

- **Severity**: WARNING
- **Impact**: HIGH - architectural stakes; think carefully before deciding
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Data/CompatibilityDbContext.cs:43`
- **Detail**: The plan requires claim-level source traceability and duplicate-resistant source references. The implementation has separate claim FKs for Game, SourceSystem, and SourceReference, but the database does not enforce that the cited SourceReference belongs to the same game/source system as the claim. The SourceReference uniqueness index also includes GameId, so the same external source identity can be attached to multiple games.
- **Fix A Recommended**: Normalize citation ownership through SourceReference.
  - Strength: Removes redundant claim/source-system state and makes the citation row the single source of truth for source URL, source ID, and source system.
  - Tradeoff: Requires an EF model change, migration, read model adjustment, and a PostgreSQL regression test.
  - Confidence: HIGH - SourceReference already contains GameId, SourceSystemId, URL, source-native ID, and metadata.
  - Blind spot: Existing seed rows are simple; no importer exists yet to reveal caller impact.
- **Fix B**: Keep the current shape and add composite constraints.
  - Strength: Preserves the direct EvidenceClaim.SourceSystem relationship.
  - Tradeoff: More complex EF mapping and a higher chance of future inconsistency.
  - Confidence: MED - EF can model this, but it is heavier than the current baseline needs.
  - Blind spot: Need to validate EF migration output for the composite relationship.
- **Decision**: FIXED via Fix A - normalized citation ownership through SourceReference.

### F2 - Local Postgres publishes on all host interfaces

- **Severity**: WARNING
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `docker-compose.yml:8`
- **Detail**: The Compose service maps `5433:5432`, which binds on all host interfaces by default, while using a known checked-in development password.
- **Fix**: Change the port mapping to `127.0.0.1:5433:5432`.
- **Decision**: FIXED - bound the local Postgres port to localhost only.

### F3 - Visible game listing is unbounded

- **Severity**: WARNING
- **Impact**: MEDIUM - real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/GameCompatibilityReadService.cs:8`
- **Detail**: `GetVisibleGamesAsync` returns every visible game without a limit or paging contract. This is fine for the current seed catalog, but the service is the future browse-list foundation and differs from the search method, which already has a limit.
- **Fix**: Add a bounded list contract, such as `limit`/`offset` with a sane default cap.
  - Strength: Prevents the first browse UI from inheriting an unbounded query.
  - Tradeoff: Changes the service interface and tests before there is a public UI.
  - Confidence: MED - the plan names a small MVP catalog now, but later browse/search work will naturally grow this path.
  - Blind spot: No product decision yet on page size or ordering beyond title.
- **Decision**: FIXED - added a bounded limit/offset visible-game list contract.

### F4 - Detail query may multiply rows as evidence grows

- **Severity**: OBSERVATION
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/GameCompatibilityReadService.cs:46`
- **Detail**: The detail query includes multiple collection navigations in one EF query. As source references and claims grow, this can create row multiplication.
- **Fix**: Add `.AsSplitQuery()` to the detail query or project directly into read models.
- **Decision**: FIXED - added split-query loading for game detail.

## Verification

- `dotnet build LinuxGameCompat.sln --no-restore`: PASS outside sandbox after MSBuild pipe creation was blocked by sandbox permissions. 0 warnings, 0 errors.
- `dotnet test LinuxGameCompat.sln --no-restore`: PASS. 17 passed, 0 failed, 0 skipped.
- `dotnet ef migrations list --project LinuxGameCompat/LinuxGameCompat.csproj --no-build`: PASS. Found `20260528143249_InitialCreate`, `20260528165859_EvidenceDomainSeedData`, and `20260530174939_NormalizeEvidenceClaimCitation`.
- `dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj --no-build`: PASS. Applied `20260530174939_NormalizeEvidenceClaimCitation` to the local configured database.
