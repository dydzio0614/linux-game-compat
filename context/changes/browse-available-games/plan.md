# Browse Available Games Implementation Plan

## Overview

Add a dedicated anonymous catalog browse page so visitors can view available games without submitting a search phrase. The page will reuse the existing visible-game read service, match the current search-result row pattern, support simple next/previous paging, and link into the existing game detail pages.

## Current State Analysis

The app already has the backend contract needed for browsing. `GameCompatibilityReadService.GetVisibleGamesAsync(limit, offset, cancellationToken)` returns visible games ordered by title, excludes hidden records, clamps negative offsets, and caps large limits. The root `Home.razor` page is search-first only and renders `GameListItem` rows from `SearchVisibleGamesByTitleAsync`. `GameDetail.razor` owns `/games/{Slug}` and renders source-backed evidence, summaries, and no-evidence uncertainty states.

What is missing is a public route that calls the visible-games list contract without a search query and a navigation entry that exposes it.

## Desired End State

Anonymous visitors can open `/games`, see a paged list of visible catalog games, compare compact compatibility status at a glance, and open each game's existing detail page. The root lookup page remains search-first. Hidden records are still excluded through the read service, and detail rendering remains centralized in `/games/{slug}`.

### Key Discoveries:

- `LinuxGameCompat/Services/GameCompatibilityReadService.cs` already exposes bounded `GetVisibleGamesAsync(limit, offset)` behavior for browse.
- `LinuxGameCompat/Components/Pages/Home.razor` already defines the row content pattern for title, status, optional Steam App ID, and detail link.
- `LinuxGameCompat/Components/Pages/GameDetail.razor` already handles `/games/{Slug}` detail behavior, including source links and no-evidence states.
- `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs` already verifies visible-game listing, hidden-game exclusion, and bounded list behavior.

## What We're NOT Doing

- No schema, migration, seed data, or EF model changes.
- No new service interface unless implementation discovers the current list contract is insufficient.
- No filters, status facets, search suggestions, or sort controls.
- No richer browse row summaries, evidence counts, or source snippets.
- No auth, favorites, admin tooling, external source crawling, or generated summaries.
- No new UI automation framework for this slice.

## Implementation Approach

Add a small Blazor page at `/games` that consumes `IGameCompatibilityReadService.GetVisibleGamesAsync`. Request one extra row (`PageSize + 1`) to determine whether a next page exists while rendering only `PageSize` rows. Keep the row layout aligned with search results and link each row to the existing detail route. Add a sidebar navigation item and minimal shared CSS for paging controls and browse-page spacing.

## Phase 1: Browse Page Route And List UI

### Overview

Create the dedicated browse route and render the first page of visible games using the existing list read model.

### Changes Required:

#### 1. Browse Page

**File**: `LinuxGameCompat/Components/Pages/Games.razor`

**Intent**: Add the public `/games` page and load visible catalog entries without requiring a search query. The page should be interactive server-rendered like the current lookup page so paging state can update without adding API endpoints.

**Contract**: Define `@page "/games"`, inject `IGameCompatibilityReadService`, use a constant page size of `20`, and render `GameListItem` rows with title, `CompatibilityStatusLabels.ToPublicLabel`, optional Steam App ID, and a `View details` link to `/games/{game.Slug}`.

#### 2. Initial Loading And Empty State

**File**: `LinuxGameCompat/Components/Pages/Games.razor`

**Intent**: Make the first page load state and no-catalog state explicit so users are not left with a blank page.

**Contract**: Track loading state around the service call. When no visible games are returned for the first page, show an empty state explaining that no public catalog games are available yet.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- Opening `/games` shows the first page of visible games ordered by title.
- Game rows show title, compact status, optional Steam App ID, and a detail link.
- A visible game's detail link opens the existing `/games/{slug}` detail page.
- If no visible games exist, the browse page shows an understandable empty state.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets; the corresponding checkboxes live in the `## Progress` section.

---

## Phase 2: Paging, Navigation, And Shared Styling

### Overview

Add simple next/previous paging, expose the page in navigation, and ensure the browse list fits the existing responsive UI.

### Changes Required:

#### 1. Offset Paging Controls

**File**: `LinuxGameCompat/Components/Pages/Games.razor`

**Intent**: Let the browse page handle catalogs larger than one page without adding filters or new backend contracts.

**Contract**: Keep an `offset` state value. Load `PageSize + 1` rows, render at most `PageSize`, set `hasNextPage` from the extra row, disable previous when `offset == 0`, and disable next when no extra row was returned. Previous decrements by `PageSize` without going below zero; next increments by `PageSize`.

#### 2. Navigation Entry

**File**: `LinuxGameCompat/Components/Layout/NavMenu.razor`

**Intent**: Make browsing discoverable while keeping lookup as the root search-first path.

**Contract**: Add a `NavLink` labeled `Games` pointing to `games`. Leave the existing `Lookup` link as the root route with `NavLinkMatch.All`.

#### 3. Shared Browse Styling

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Keep browse visually consistent with search results and prevent button/list overflow on mobile.

**Contract**: Reuse existing `.result-list`, `.result-item`, `.result-meta`, `.status-label`, and `.empty-state` classes. Add only minimal classes for the browse page wrapper and paging controls when existing classes are insufficient.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- Sidebar navigation includes `Games` and marks it active on `/games`.
- Previous is disabled on the first page.
- Next is enabled only when another page exists.
- Paging changes the visible rows without breaking detail links.
- Browse page remains usable on narrow mobile widths with no clipped row content or overflowing buttons.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Verification And Handoff

### Overview

Confirm the browse slice works end to end and document that it relies on existing backend contracts rather than new persistence or API changes.

### Changes Required:

#### 1. Regression Verification

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Preserve existing service-level guarantees that browse relies on.

**Contract**: Do not add tests unless implementation changes the read-service contract. Existing tests should continue covering visible-game listing, hidden-game exclusion, bounded limits, and offset behavior.

#### 2. Manual Smoke Checklist

**File**: `context/changes/browse-available-games/plan.md`

**Intent**: Give implementers and reviewers the exact user-visible behaviors to verify because the repo does not currently have UI automation.

**Contract**: Keep the manual test steps in this plan aligned with the implemented route, nav item, paging behavior, detail links, empty state, and mobile layout.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- `/games` loads as an anonymous visitor.
- `/` remains the search-first lookup page.
- Browse, lookup, and detail pages can be reached through normal links.
- Hidden records do not appear in browse results.
- No new database migration is generated for this change.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before marking the change complete.

---

## Testing Strategy

### Unit Tests:

- No new unit tests are required for a page-only implementation that keeps the existing read-service contract unchanged.
- If paging logic is extracted into a helper, test first-page, middle-page, last-page, and empty-list state calculations.

### Integration Tests:

- Existing PostgreSQL integration tests should continue covering visible-game list behavior, hidden-game exclusion, and bounded offset reads.
- Add integration tests only if the service contract changes.

### Manual Testing Steps:

1. Start the app and open `/games`.
2. Confirm visible games render in title order.
3. Confirm each row shows title, status label, optional Steam App ID, and `View details`.
4. Open a detail link and confirm the existing evidence page renders.
5. Use next and previous controls with enough records to span multiple pages.
6. Confirm previous is disabled on the first page and next is disabled on the last page.
7. Confirm `/` still renders the search-first lookup page.
8. Confirm the sidebar exposes both `Lookup` and `Games`.
9. Check the browse page at a narrow mobile width for wrapping and button overflow.

## Performance Considerations

The browse page should request a bounded page of at most `PageSize + 1` records and reuse the existing `AsNoTracking` list query. No full-catalog load, client-side filtering, or detail-query fan-out should be introduced.

## Migration Notes

No database migration is expected. If an implementation generates a migration, stop and reassess because this slice should not change persistence.

## References

- Product requirement: `context/foundation/prd.md` FR-005.
- Roadmap slice: `context/foundation/roadmap.md` S-03.
- Existing list service: `LinuxGameCompat/Services/GameCompatibilityReadService.cs`.
- Existing search row pattern: `LinuxGameCompat/Components/Pages/Home.razor`.
- Existing detail route: `LinuxGameCompat/Components/Pages/GameDetail.razor`.
- Existing service tests: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Browse Page Route And List UI

#### Automated

- [ ] 1.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 1.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual

- [ ] 1.3 Opening `/games` shows the first page of visible games ordered by title.
- [ ] 1.4 Game rows show title, compact status, optional Steam App ID, and a detail link.
- [ ] 1.5 A visible game's detail link opens the existing `/games/{slug}` detail page.
- [ ] 1.6 If no visible games exist, the browse page shows an understandable empty state.

### Phase 2: Paging, Navigation, And Shared Styling

#### Automated

- [ ] 2.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 2.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual

- [ ] 2.3 Sidebar navigation includes `Games` and marks it active on `/games`.
- [ ] 2.4 Previous is disabled on the first page.
- [ ] 2.5 Next is enabled only when another page exists.
- [ ] 2.6 Paging changes the visible rows without breaking detail links.
- [ ] 2.7 Browse page remains usable on narrow mobile widths with no clipped row content or overflowing buttons.

### Phase 3: Verification And Handoff

#### Automated

- [ ] 3.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 3.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual

- [ ] 3.3 `/games` loads as an anonymous visitor.
- [ ] 3.4 `/` remains the search-first lookup page.
- [ ] 3.5 Browse, lookup, and detail pages can be reached through normal links.
- [ ] 3.6 Hidden records do not appear in browse results.
- [ ] 3.7 No new database migration is generated for this change.
