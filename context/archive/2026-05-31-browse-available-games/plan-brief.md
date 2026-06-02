# Browse Available Games - Plan Brief

> Full plan: `context/changes/browse-available-games/plan.md`

## What & Why

Add a dedicated anonymous browse page for the available game catalog. This satisfies FR-005 by letting visitors inspect catalog entries without first submitting a title search, while preserving the existing search-first lookup flow.

## Starting Point

The backend already exposes `GetVisibleGamesAsync(limit, offset)` and existing tests cover visible-game listing, hidden-game exclusion, and bounded offsets. The UI has a search-first home page and existing `/games/{slug}` detail pages, but no no-query browse route.

## Desired End State

Visitors can open `/games`, browse visible games in pages, compare compact compatibility status, and open each game's existing detail page. The browse page uses the same row content as search results and does not introduce new schema, service, or detail-rendering behavior.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Browse surface | Dedicated `/games` page | Keeps the root page focused on direct lookup while giving browse a clear URL. |
| Paging | Simple next/previous | Uses the existing limit/offset service and handles catalog growth beyond the seed data. |
| Search relationship | Separate pages with light links | Avoids refactoring current search state into a combined component. |
| Row content | Match search rows | Reuses established title/status/Steam ID/detail-link presentation. |
| Backend changes | No contract changes | `GetVisibleGamesAsync` already provides the needed behavior. |
| Testing | Build/tests plus manual smoke | Matches current repo patterns without adding UI automation for a small slice. |

## Scope

**In scope:**

- Dedicated `/games` browse page.
- Visible-game list rendering.
- Title/status/Steam ID/detail-link rows.
- Simple next/previous paging.
- Sidebar navigation entry.
- Minimal shared styling.
- Automated build/test verification plus manual smoke checks.

**Out of scope:**

- Filters, sorting controls, search suggestions, or unified search/browse state.
- Rich summaries, evidence counts, or source snippets in browse rows.
- Schema, migration, seed data, or service-contract changes.
- Auth, favorites, admin tooling, external crawling, or generated summaries.
- New UI automation framework.

## Architecture / Approach

Implement browse as a Blazor interactive server page that calls `IGameCompatibilityReadService.GetVisibleGamesAsync(PageSize + 1, offset)`. Render at most `PageSize` rows and use the extra row only to decide whether the next button is enabled. Navigation links users to `/games`, while detail links continue to use the existing `/games/{slug}` page.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Browse Page Route And List UI | `/games` route and first-page visible-game rows | Blank or unclear state during load/empty catalog. |
| 2. Paging, Navigation, And Shared Styling | Next/previous controls, sidebar entry, responsive layout | Paging state or mobile layout could feel rough. |
| 3. Verification And Handoff | Build/test/manual smoke confirmation | UI behavior relies on manual smoke because no UI test framework exists. |

**Prerequisites:** Existing F-01/S-01 read service and detail page are present.
**Estimated effort:** About 1 focused implementation session across 3 phases.

## Open Risks & Assumptions

- The catalog can remain title-ordered with no explicit sort controls.
- Page size `20` is acceptable for the first browse version.
- Existing service tests are sufficient unless the service contract changes.
- No migration should be generated.

## Success Criteria (Summary)

- Anonymous visitors can browse visible games at `/games`.
- Browse rows match search-result rows and link to existing details.
- Paging works without showing hidden records.
- `dotnet build LinuxGameCompat.sln --no-restore` and `dotnet test LinuxGameCompat.sln --no-restore` pass.
