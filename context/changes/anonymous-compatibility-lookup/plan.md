# Anonymous Compatibility Lookup Implementation Plan

## Overview

Build the first anonymous lookup flow for the Linux game compatibility app. A visitor can search for a game title from the home page, open a game detail page, and see a compact compatibility status with source-linked evidence, caveats, workarounds, and explicit uncertainty when no curated evidence exists.

This change builds on the completed `minimal-evidence-baseline` backend foundation. It keeps the existing read-service boundary, hardens public search behavior, and replaces starter Blazor UI with the lookup experience.

## Current State Analysis

The app has a PostgreSQL-backed compatibility evidence model and read service from F-01. `IGameCompatibilityReadService` already exposes visible-game search and slug detail lookup, with hidden records excluded and no-evidence games represented as valid `Unknown` records.

The public UI is still the default Blazor starter shell. The root page says "Hello, world!", navigation still links to Counter and Weather, and no page consumes the compatibility read service yet.

## Desired End State

Anonymous users can search for visible games from `/`, select a result, and view `/games/{slug}` for source-linked compatibility evidence. Public status labels use the existing internal status contract, no-evidence records are shown honestly as `Unknown`, and hidden or missing records share one generic not-found state.

### Key Discoveries:

- `IGameCompatibilityReadService` already provides `SearchVisibleGamesByTitleAsync` and `GetVisibleGameBySlugAsync` for S-01.
- `GameCompatibilityReadService` currently uses PostgreSQL `ILIKE` with unescaped user input, so `%` and `_` behave as SQL wildcards.
- `GameDetail` already includes source references, evidence claims, and optional summary data for a detail page.
- `CompatibilityStatus` is intentionally internal and minimal: `Unknown`, `Unsupported`, `PlayableWithCaveats`, and `Playable`.
- Hidden games are excluded from list, search, and detail read paths by default.
- No-evidence games are valid and should surface as `Unknown`, not as an error.
- The current Blazor UI is starter content and has no established UI test framework.

## What We're NOT Doing

- No auth, member favorites, account model, or admin tooling.
- No browse page or default visible-game catalog list.
- No live ProtonDB, Are We Anti-Cheat Yet, or broad source API calls.
- No GPT summary generation, prompt work, background jobs, retries, or cost controls.
- No database schema redesign or migration unless needed by implementation discoveries.
- No new bUnit, Playwright, or browser automation framework in this slice.
- No personalized compatibility by hardware, distribution, settings, or game library.

## Implementation Approach

Keep the existing EF/read-service backend boundary and build the UI against `IGameCompatibilityReadService`. Make the smallest backend adjustment needed for public search safety: escape `LIKE` wildcard characters before passing title search input to PostgreSQL. Then replace the starter Blazor pages with a focused lookup home page and evidence-first game detail page.

## Critical Implementation Details

### Search wildcard handling

Public title search must treat `%`, `_`, and `\` as literal user input. Use Npgsql's `EF.Functions.ILike(match, pattern, escapeCharacter)` overload and escape backslash first, then `%`, then `_`, before wrapping the escaped query with `%...%`.

### UI data boundary

Razor pages should consume `IGameCompatibilityReadService`; they should not query `CompatibilityDbContext` directly. This preserves the F-01 hidden-game and no-evidence behavior in one place.

## Phase 1: Search Contract Hardening

### Overview

Make anonymous title search predictable before exposing it publicly.

### Changes Required:

#### 1. Literal Title Search

**File**: `LinuxGameCompat/Services/GameCompatibilityReadService.cs`

**Intent**: Escape user-entered SQL `LIKE` wildcard characters so public search behaves like literal title search.

**Contract**: Keep `SearchVisibleGamesByTitleAsync` signature unchanged. Preserve trimming, empty-query behavior, visible-only filtering, title ordering, and the existing limit cap. Use an escape character with `EF.Functions.ILike`.

#### 2. Search Regression Tests

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Lock the literal-search behavior against PostgreSQL so later refactors do not reintroduce wildcard matching.

**Contract**: Add integration tests with dedicated visible test rows whose titles contain `%`, `_`, and `\`. Prove each character can be found as literal input, those searches do not return unrelated visible games, normal case-insensitive title search still works, and hidden games remain excluded.

### Success Criteria:

#### Automated Verification:

- A title containing `%` can be found by searching the literal `%` character and does not return unrelated visible games.
- A title containing `_` can be found by searching the literal `_` character and does not return unrelated visible games.
- A title containing `\` can be found by searching the literal `\` character and does not return unrelated visible games.
- Existing `gate` title search still finds `baldurs-gate-3`.
- Hidden games remain excluded from title search.
- Full test suite passes: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- No manual verification is required for this backend-only phase.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Lookup Home Page

### Overview

Replace the starter root page with the primary anonymous lookup workflow.

### Changes Required:

#### 1. Search-First Home Page

**File**: `LinuxGameCompat/Components/Pages/Home.razor`

**Intent**: Turn `/` into the anonymous game lookup entry point.

**Contract**: Use interactive server rendering and inject `IGameCompatibilityReadService`. Initial page state shows a focused search form only, not a default catalog list. Submitted searches call `SearchVisibleGamesByTitleAsync`.

#### 2. Search Result Rendering

**File**: `LinuxGameCompat/Components/Pages/Home.razor`

**Intent**: Present compact results that help a user choose the matching game.

**Contract**: Each result shows title, public status label, optional Steam App ID, and a link to `/games/{slug}`. Empty search results render a clear empty state.

#### 3. Public Status Label Helper

**File**: `LinuxGameCompat/Components/CompatibilityStatusLabels.cs`

**Intent**: Keep public status labels consistent between list and detail UI.

**Contract**: Add a UI-layer static helper used by both Home and GameDetail. Map `Unknown` to `Unknown`, `Unsupported` to `Unsupported`, `PlayableWithCaveats` to `Playable with caveats`, and `Playable` to `Playable`. Do not rename or change the enum.

### Success Criteria:

#### Automated Verification:

- Project builds: `dotnet build LinuxGameCompat.sln --no-restore`
- Full test suite passes: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- `/` loads a search-first lookup page, not starter content.
- Empty search does not show stale or default results.
- Searching `gate` shows Baldur's Gate 3 with a status label and detail link.
- Search result links point to `/games/{slug}`.
- Searching for `%` or `_` in the UI does not produce surprising broad results.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Game Detail Evidence Page

### Overview

Add the source-linked detail page that proves the product value beyond a compact status.

### Changes Required:

#### 1. Game Detail Route

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`

**Intent**: Add a public detail route for selected visible games.

**Contract**: Route is `/games/{slug}`. Load data through `GetVisibleGameBySlugAsync`. Missing and hidden games render the same generic not-found state.

#### 2. Evidence-First Detail Layout

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`

**Intent**: Surface the source-backed reasoning, caveats, workarounds, and links behind the compatibility status.

**Contract**: Render title, public status label, grouped evidence claims by `Status`, `Caveat`, `Workaround`, and `Note`, and source links attached to claims. Include a source-reference section so users can inspect supporting sources.

#### 3. No-Evidence And Summary States

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`

**Intent**: Avoid false certainty when evidence is incomplete while still using already stored summary data when helpful.

**Contract**: For `Unknown` games with no claims, show an explicit "no source-backed evidence has been curated yet" message. Stored summary text may appear as supporting text when present, but raw source-linked evidence remains the primary content.

### Success Criteria:

#### Automated Verification:

- Project builds: `dotnet build LinuxGameCompat.sln --no-restore`
- Full test suite passes: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- `/games/baldurs-gate-3` shows status, source-linked evidence, and source references.
- `/games/unnamed-prototype` shows `Unknown` and explicit no-evidence messaging.
- `/games/suppressed-test-record` shows the generic not-found state.
- A made-up slug shows the same generic not-found state.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Starter UI Cleanup And Handoff

### Overview

Remove template UI from the public surface and document the completed lookup slice.

### Changes Required:

#### 1. Starter Page Cleanup

**File**: `LinuxGameCompat/Components/Pages/Counter.razor`, `LinuxGameCompat/Components/Pages/Weather.razor`

**Intent**: Remove starter-only routes from the app so production UI is focused on compatibility lookup.

**Contract**: Delete the starter Counter and Weather pages unless implementation finds a project convention requiring a softer removal.

#### 2. Navigation And Shell Copy

**File**: `LinuxGameCompat/Components/Layout/NavMenu.razor`, `LinuxGameCompat/Components/Layout/MainLayout.razor`

**Intent**: Replace template navigation and header copy with lookup-focused public app copy.

**Contract**: Navigation should point users to the lookup home page. Remove Counter and Weather links. Keep the existing layout structure and responsive behavior.

#### 3. Styling

**File**: `LinuxGameCompat/wwwroot/app.css` and/or page-scoped `.razor.css` files

**Intent**: Add restrained, readable styling for search, statuses, evidence groups, and source links.

**Contract**: Follow the existing Bootstrap/scoped CSS setup. Avoid large visual redesign outside the lookup flow.

### Success Criteria:

#### Automated Verification:

- Project builds: `dotnet build LinuxGameCompat.sln --no-restore`
- Full test suite passes: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- Counter and Weather starter pages are no longer exposed through navigation.
- Direct `/counter` and `/weather` requests show the generic not-found state.
- The app shell no longer presents starter template copy.
- Lookup and detail pages remain usable on desktop and mobile widths.
- Source links are visible and clickable from detail evidence.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to plan closeout.

---

## Testing Strategy

### Unit Tests:

- No new pure unit test layer is required unless the status label helper grows beyond a simple mapping.
- If status mapping is extracted into C#, test every `CompatibilityStatus` value maps to the agreed public label.

### Integration Tests:

- PostgreSQL title search treats `%` literally.
- PostgreSQL title search treats `_` literally.
- PostgreSQL title search treats `\` literally.
- Normal case-insensitive title search still works.
- Hidden records remain excluded from public search and detail.
- Existing no-evidence detail behavior remains intact.

### Manual Testing Steps:

1. Start the app with the local development database available.
2. Open `/` and verify the search-first page appears.
3. Submit an empty search and verify no stale/default results appear.
4. Search `gate` and verify Baldur's Gate 3 appears with `Playable` and a detail link.
5. Open `/games/baldurs-gate-3` and verify source-linked evidence renders.
6. Open `/games/unnamed-prototype` and verify `Unknown` plus no-evidence messaging.
7. Open `/games/suppressed-test-record` and a made-up slug; verify both show generic not found.
8. Check the app on a narrow viewport and verify search/results/detail content does not overlap or clip.

## Performance Considerations

The slice uses bounded title search through the existing service limit cap. No default all-games list is rendered on the home page, which keeps the first public flow aligned with the PRD's lookup-first path and avoids introducing browse pagination in S-01.

## Migration Notes

No database migration is expected. If implementation discovers a required schema change, stop and update this plan before proceeding.

## References

- PRD: `context/foundation/prd.md`
- Roadmap: `context/foundation/roadmap.md`
- Prior foundation plan: `context/changes/minimal-evidence-baseline/plan.md`
- Prior foundation brief: `context/changes/minimal-evidence-baseline/plan-brief.md`
- Prior foundation implementation notes: `context/changes/minimal-evidence-baseline/implementation-notes.md`
- Prior implementation review: `context/changes/minimal-evidence-baseline/reviews/impl-review.md`
- Read-service contract: `LinuxGameCompat/Services/IGameCompatibilityReadService.cs`
- Read-service implementation: `LinuxGameCompat/Services/GameCompatibilityReadService.cs`
- Read models: `LinuxGameCompat/Services/GameReadModels.cs`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Search Contract Hardening

#### Automated

- [x] 1.1 A title containing `%` can be found by searching the literal `%` character and does not return unrelated visible games — e0b492e
- [x] 1.2 A title containing `_` can be found by searching the literal `_` character and does not return unrelated visible games — e0b492e
- [x] 1.3 A title containing `\` can be found by searching the literal `\` character and does not return unrelated visible games — e0b492e
- [x] 1.4 Existing `gate` title search still finds `baldurs-gate-3` — e0b492e
- [x] 1.5 Hidden games remain excluded from title search — e0b492e
- [x] 1.6 Full test suite passes — e0b492e

#### Manual

- [x] 1.7 No manual verification is required for this backend-only phase — e0b492e

### Phase 2: Lookup Home Page

#### Automated

- [x] 2.1 Project builds — 3002f33
- [x] 2.2 Full test suite passes — 3002f33

#### Manual

- [x] 2.3 `/` loads a search-first lookup page, not starter content — 3002f33
- [x] 2.4 Empty search does not show stale or default results — 3002f33
- [x] 2.5 Searching `gate` shows Baldur's Gate 3 with a status label and detail link — 3002f33
- [x] 2.6 Search result links point to `/games/{slug}` — 3002f33
- [x] 2.7 Searching for `%` or `_` in the UI does not produce surprising broad results — 3002f33

### Phase 3: Game Detail Evidence Page

#### Automated

- [x] 3.1 Project builds — 12b4a3a
- [x] 3.2 Full test suite passes — 12b4a3a

#### Manual

- [x] 3.3 `/games/baldurs-gate-3` shows status, source-linked evidence, and source references — 12b4a3a
- [x] 3.4 `/games/unnamed-prototype` shows `Unknown` and explicit no-evidence messaging — 12b4a3a
- [x] 3.5 `/games/suppressed-test-record` shows the generic not-found state — 12b4a3a
- [x] 3.6 A made-up slug shows the same generic not-found state — 12b4a3a

### Phase 4: Starter UI Cleanup And Handoff

#### Automated

- [x] 4.1 Project builds
- [x] 4.2 Full test suite passes

#### Manual

- [x] 4.3 Counter and Weather starter pages are no longer exposed through navigation
- [x] 4.4 Direct `/counter` and `/weather` requests show the generic not-found state
- [x] 4.5 The app shell no longer presents starter template copy
- [x] 4.6 Lookup and detail pages remain usable on desktop and mobile widths
- [x] 4.7 Source links are visible and clickable from detail evidence
