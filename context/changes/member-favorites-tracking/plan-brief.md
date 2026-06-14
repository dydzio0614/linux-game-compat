# Member Favorites Tracking - Plan Brief

> Full plan: `context/changes/member-favorites-tracking/plan.md`

## What & Why

Build roadmap S-02: logged-in members can save games to favorites and view a personal list with current compatibility status. Favorites matter because they are more useful than browser bookmarks: they reflect the app's latest compatibility status for saved games.

## Starting Point

The app already has public lookup/detail/browse, PostgreSQL game data, passwordless member access, auth-aware navigation, and a current-member accessor. It does not yet have a favorites table, service, favorite controls, or `/favorites` page.

## Desired End State

Members add or remove a visible game from `/games/{slug}` and see saved visible games at `/favorites`, sorted by title with current status labels. Anonymous users still access public game detail pages and see a login-to-favorite CTA that returns them to the game after sign-in.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Favoriteable games | Visible games only | Preserves hidden/suppressed record privacy. |
| Actions | Add and remove | Keeps accidental saves reversible. |
| Anonymous detail behavior | Login CTA | Makes favorites discoverable without blocking anonymous lookup. |
| Add surface | Detail page only | Matches roadmap scope and avoids list-row state across search/browse. |
| Favorites ordering | Title A-Z | Makes the personal list scannable. |
| Mutation feedback | Inline state update | Fits Blazor interactive pages and keeps users in context. |
| Tracking fields | `CreatedAt` only | Enough for ownership and future recency without analytics scope. |
| Duplicate/concurrent behavior | Idempotent success | Handles retries, double-clicks, and multiple tabs cleanly. |

## Scope

**In scope:**

- `MemberFavorite` persistence model and EF migration.
- Scoped member favorites service using `ICurrentMemberAccessor`.
- Idempotent add/remove and A-Z visible-game list behavior.
- Detail-page favorite controls and anonymous login CTA.
- Protected `/favorites` page and signed-in nav link.
- PostgreSQL integration tests for schema, ownership, hidden-game exclusion, idempotency, and current status.

**Out of scope:**

- Favorite controls on search or browse rows.
- Event history, analytics, popularity reports, or refresh-priority implementation.
- Profile/account management, recovery, deletion, email change, roles, admin, OAuth, or passkeys.
- Favorites pagination.
- New browser/component test framework.
- Magic-link request throttling.

## Architecture / Approach

Add a dedicated `IMemberFavoritesService` instead of expanding `IGameCompatibilityReadService`. The public read service remains responsible for public game/evidence reads; the favorites service owns member scoping, visible-game enforcement, idempotent mutations, and list projection to existing `GameListItem`-style rows.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Favorites Persistence And Service Contract | Table, migration, service, DI, core behavior, integration coverage | Owner isolation and hidden-game filtering must be correct. |
| 2. Detail And Favorites UI | Detail controls, `/favorites`, nav, inline feedback | UI must not block anonymous lookup or leak member data. |
| 3. Documentation And Final Manual Verification | Docs and final smoke | Final verification must catch route, auth, and copy regressions. |

**Prerequisites:** Existing S-01 anonymous lookup/detail and F-02 passwordless member access remain in place.
**Estimated effort:** ~2-3 focused implementation sessions across 3 phases.

## Open Risks & Assumptions

- Hidden games already saved by a member remain in the table but are excluded from member UI.
- The first favorites list does not paginate; this is acceptable for MVP data volume.
- Existing auth throttling follow-up remains separate and may still matter before public or higher-volume launch.
- UI behavior relies on manual smoke because no browser/component test framework exists yet.

## Success Criteria (Summary)

- A signed-in member can add/remove visible games and see only their own favorites.
- `/favorites` shows saved visible games sorted A-Z with current compatibility status.
- Anonymous lookup/detail remains public, and hidden games do not leak through favorites behavior.
