# Member Favorites Tracking Implementation Plan

## Overview

Add roadmap S-02: logged-in members can save visible games as favorites from game detail pages and open a personal favorites page showing each saved game's current compatibility status.

This builds on the completed anonymous lookup, browse, and passwordless member-access foundations. Favorites are member-owned, owner-isolated, and visible-game-only. Anonymous users keep full public lookup/detail access and see a login call to action when they want to save a game.

## Current State Analysis

The app already has a PostgreSQL-backed game catalog, public read service, game detail page, browse page, passwordless Identity auth, auth-aware navigation, and a current-member accessor. It does not yet have a member-owned favorites table, favorites service, favorite controls, or `/favorites` route.

The existing public read service centralizes visible-game filtering through `!IsHidden`, and pages consume read models rather than querying `CompatibilityDbContext` directly. Favorites should preserve that boundary: member-specific logic belongs in a new member favorites service, while game visibility and status semantics should remain consistent with the existing game read contracts.

## Desired End State

Signed-in members can add a visible game to favorites from `/games/{slug}`, remove it from either the detail page or `/favorites`, and see a personal favorites list sorted by game title. The list shows current compatibility status using the existing public status labels.

Anonymous users can still view public game details without logging in. They see a small login-to-favorite call to action that routes through `/login?returnUrl=/games/{slug}`.

### Key Discoveries:

- `CompatibilityDbContext` has games, evidence, summaries, Identity users, and magic-link requests, but no member-owned favorites table.
- `ApplicationUser` is the local Identity user type; archived auth planning says future favorites attach to local user id, not email.
- `ICurrentMemberAccessor` already exposes the current authenticated member id/email so downstream features do not parse claims directly.
- `GameCompatibilityReadService` excludes hidden games and returns `GameListItem` rows with current `CompatibilityStatus`.
- `GameDetail.razor` already loads a visible game by slug and is the natural add/remove favorite insertion point.
- `Games.razor` and `Home.razor` already define the row/status-label UI pattern that `/favorites` should reuse.
- `context/foundation/test-plan.md` requires owner-isolation coverage when member favorites open.

## What We're NOT Doing

- No favorite controls on search results or browse rows.
- No event history, analytics dashboard, popularity reporting, or refresh-priority implementation.
- No profile management, account recovery, account deletion, email change, roles, admin, OAuth, or passkeys.
- No personalized compatibility by hardware, distro, settings, or game library.
- No pagination for the first favorites list.
- No browser/component test framework in this slice.
- No auth throttling work; the existing magic-link throttling follow-up remains separate.

## Implementation Approach

Add a dedicated member favorites persistence and service layer rather than expanding `IGameCompatibilityReadService`. The public game read service remains focused on public game/evidence reads; the new service owns authenticated member favorites, uses `ICurrentMemberAccessor`, enforces visible-game behavior, and returns `GameListItem`-style rows for UI reuse.

Use a unique `(MemberId, GameId)` constraint and idempotent add/remove semantics so double-clicks, retries, and multiple tabs converge on the requested final state. Persist only `CreatedAt` for this slice.

## Critical Implementation Details

### State Sequencing

Favorite add/remove actions must derive the member id from `ICurrentMemberAccessor` at mutation time and must re-check game visibility in the service. Do not rely only on the detail page's initial visible-game load, because the game could be hidden or the member state could change between render and click.

### User Experience Spec

The anonymous detail page remains public. The login-to-favorite action should preserve the current detail route with a local return URL so a member lands back on the game after magic-link sign-in.

## Phase 1: Favorites Persistence And Service Contract

### Overview

Add the database model, migration, service interface, implementation, DI registration, and PostgreSQL integration coverage for member-owned favorites.

### Changes Required:

#### 1. Member Favorite Entity

**File**: `LinuxGameCompat/Data/MemberFavorite.cs`

**Intent**: Define the member-owned relationship between one local member and one game.

**Contract**: Entity includes `Id`, `MemberId`, `ApplicationUser Member`, `GameId`, `Game Game`, and `CreatedAt`. `MemberId` references `ApplicationUser.Id`; do not store email.

#### 2. EF Core Mapping And Migration

**File**: `LinuxGameCompat/Data/CompatibilityDbContext.cs`

**Intent**: Add favorites to the existing PostgreSQL/Identity database boundary.

**Contract**: Add `DbSet<MemberFavorite>`. Configure required relationships to `ApplicationUser` and `Game`, cascade delete from member and game, a unique index on `(MemberId, GameId)`, and supporting indexes for member-list and game-state lookups. Generate a migration and update the model snapshot.

#### 3. Favorites Read Models And Result Contracts

**File**: `LinuxGameCompat/Services/MemberFavoriteModels.cs`

**Intent**: Give the service a small explicit contract for favorite state and mutation outcomes.

**Contract**: Include a favorite-state model for detail pages and a mutation result that can represent unauthenticated, hidden/missing game, success, and generic failure. List rows may reuse `GameListItem` directly if no extra fields beyond current status are required.

#### 4. Member Favorites Service

**File**: `LinuxGameCompat/Services/IMemberFavoritesService.cs`, `LinuxGameCompat/Services/MemberFavoritesService.cs`

**Intent**: Centralize member ownership, visible-game enforcement, idempotent mutations, and favorites listing.

**Contract**: Service supports:

- get favorite state for the current member and visible game id;
- add current member favorite for a visible game id;
- remove current member favorite for a visible game id;
- list current member favorites as visible `GameListItem` rows sorted by `Game.Title`.

Add is successful if the row already exists. Remove is successful if the row is already absent. Hidden or missing games are not favoriteable and do not appear in list results.

#### 5. Dependency Injection

**File**: `LinuxGameCompat/Program.cs`

**Intent**: Make the service available to Razor components through the existing scoped service pattern.

**Contract**: Register `IMemberFavoritesService` as scoped next to `IGameCompatibilityReadService`, `IMagicLinkService`, and `ICurrentMemberAccessor`.

#### 6. PostgreSQL Integration Tests

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Prove the persistence, member ownership, and visible-game boundaries at the same phase gate as the service implementation.

**Contract**: Add tests for schema shape, unique member-game constraint, idempotent add/remove, owner isolation, hidden-game exclusion, current-status reads, and title ordering. Use existing PostgreSQL fixture patterns.

#### 7. Auth/Favorites Harness Support

**File**: `LinuxGameCompat.Tests/AuthTestHarness.cs`

**Intent**: Reuse the existing Identity/Testcontainers setup for member-owned favorites tests.

**Contract**: Add only the minimum helper support needed to create authenticated users and resolve favorites services in tests. Do not weaken existing auth/privacy test behavior.

### Success Criteria:

#### Automated Verification:

- Migration creates `MemberFavorites` table, required columns, foreign keys, and unique `(MemberId, GameId)` index.
- Favorite add creates one row bound to the authenticated local member id.
- Duplicate favorite add is idempotent and does not create a second row.
- Favorite remove is idempotent when the row is already absent.
- Member A cannot list or remove member B's favorites.
- Hidden or missing games cannot be favorited and hidden favorited games are excluded from list results.
- Favorites list returns current `Game.CompatibilityStatus` and sorts rows by title A-Z.
- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- None for this phase; service behavior is covered through automated PostgreSQL integration tests.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation before proceeding to the next phase.

---

## Phase 2: Detail And Favorites UI

### Overview

Expose the member favorites flow in the Blazor UI while preserving anonymous public lookup/detail access.

### Changes Required:

#### 1. Game Detail Favorite Controls

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`

**Intent**: Let signed-in members add/remove the visible game they are viewing, and let anonymous users discover login-to-favorite without blocking evidence access.

**Contract**: Inject `IMemberFavoritesService`. When a visible game loads, signed-in users load favorite state and see inline add/remove controls near the game header/status metadata. Anonymous users see a login CTA linking to `/login?returnUrl=/games/{slug}`. Mutations update state in place and show concise loading, success, or error text.

#### 2. Favorites Page

**File**: `LinuxGameCompat/Components/Pages/Favorites.razor`

**Intent**: Add the member's personal favorite-games list.

**Contract**: Route `/favorites`; require authorization; use `@rendermode InteractiveServer`; inject `IMemberFavoritesService`. Load current member favorites sorted A-Z, render rows with title, current compatibility status label, optional Steam App ID, detail link, and remove action. Show an empty state that points users back to lookup or browse.

#### 3. Auth-Aware Navigation

**File**: `LinuxGameCompat/Components/Layout/NavMenu.razor`

**Intent**: Make the favorites page discoverable for signed-in members.

**Contract**: Add a `Favorites` nav link inside the existing `AuthorizeView` authorized block. Do not show it to anonymous users.

#### 4. Shared Styling

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Keep favorite controls and rows aligned with existing lookup/browse/detail visual patterns.

**Contract**: Reuse the existing result-list/result-item/status-label conventions. Add only the minimum classes needed for favorite actions, inline feedback, and mobile-safe button wrapping.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- Anonymous detail page still shows compatibility evidence without login.
- Anonymous favorite CTA routes to `/login` with a local return URL for the current game detail page.
- Signed-in detail page shows the correct add/remove favorite state.
- Adding a favorite updates the detail page inline.
- Removing a favorite updates the detail page inline.
- `/favorites` shows only the signed-in member's visible favorite games in title order.
- Removing from `/favorites` updates the list inline.
- Signed-in nav shows `Favorites`; anonymous nav does not.
- Favorites UI remains usable on narrow mobile widths without clipped text or overflowing buttons.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation before proceeding to the next phase.

---

## Phase 3: Documentation And Final Manual Verification

### Overview

Close out the completed slice with documentation updates and final smoke verification.

### Changes Required:

#### 1. Project Documentation

**File**: `README.md`

**Intent**: Reflect that member favorites are now implemented MVP functionality.

**Contract**: Update feature/planned-work wording to mention saved favorite games with current compatibility status. Do not add operational docs beyond existing setup/test instructions unless implementation introduces new configuration, which is not expected.

#### 2. Change Metadata

**File**: `context/changes/member-favorites-tracking/change.md`

**Intent**: Keep the change record aligned with the implemented plan.

**Contract**: Status remains `planned` until implementation begins; implementation should update status/progress according to the established 10x workflow.

### Success Criteria:

#### Automated Verification:

- All Phase 1 and Phase 2 automated checks pass.
- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- Full login-to-favorite smoke flow works using the development logging email sender.
- Logout hides member favorites navigation and prevents direct `/favorites` access through the existing auth flow.
- Existing lookup, browse, and detail flows still work for anonymous users.
- No UI copy implies social features, profile management, or personalized compatibility.

**Implementation Note**: After completing this phase and all automated verification passes, run the full manual smoke checklist before marking the change complete.

---

## Testing Strategy

### Unit Tests:

- Keep unit-style tests limited to pure contracts if small helper methods are introduced.
- Do not add brittle UI snapshots or duplicate EF assertions in unit tests.

### Integration Tests:

- Prefer PostgreSQL-backed service/integration tests because the risk lives in persistence constraints, Identity foreign keys, member scoping, and hidden-game filtering.
- Cover:
  - migration/table/index shape;
  - unique member-game constraint;
  - idempotent add/remove behavior;
  - owner-isolated list and remove behavior;
  - hidden-game exclusion;
  - current compatibility status reads;
  - A-Z ordering.

### Manual Testing Steps:

1. Start PostgreSQL with `docker compose up -d`.
2. Run the app with `dotnet run --project LinuxGameCompat/LinuxGameCompat.csproj`.
3. Open a visible game detail page anonymously and confirm evidence still renders.
4. Click the login-to-favorite CTA and request a magic link.
5. Use the development logged link to sign in and return to the same game detail page.
6. Add the game to favorites and confirm inline state changes.
7. Open `/favorites` and confirm the game appears with current status.
8. Remove the game from `/favorites` and confirm the list updates.
9. Add two or more games and confirm A-Z ordering.
10. Log out and confirm member nav is hidden and `/favorites` is no longer accessible.

## Performance Considerations

MVP favorite volumes are small. The unique `(MemberId, GameId)` index and member-list index are enough for the expected low-QPS app. The service should query only visible games for list output and should project list rows rather than loading evidence/source collections.

## Migration Notes

The migration adds a new `MemberFavorites` table only. No existing data needs backfill. Existing members start with empty favorites. If a game or user is deleted, cascade delete their favorite rows to avoid orphaned member-owned data.

## References

- Roadmap S-02: `context/foundation/roadmap.md`
- PRD US-02 / FR-007 / FR-008: `context/foundation/prd.md`
- Future favorites test guidance: `context/foundation/test-plan.md`
- Auth foundation: `context/archive/2026-05-31-passwordless-member-access/plan.md`
- Public lookup foundation: `context/archive/2026-05-31-anonymous-compatibility-lookup/plan.md`
- Existing read service: `LinuxGameCompat/Services/GameCompatibilityReadService.cs`
- Existing game detail route: `LinuxGameCompat/Components/Pages/GameDetail.razor`
- Existing browse row pattern: `LinuxGameCompat/Components/Pages/Games.razor`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Favorites Persistence And Service Contract

#### Automated

- [ ] 1.1 Migration creates `MemberFavorites` table, required columns, foreign keys, and unique `(MemberId, GameId)` index.
- [ ] 1.2 Favorite add creates one row bound to the authenticated local member id.
- [ ] 1.3 Duplicate favorite add is idempotent and does not create a second row.
- [ ] 1.4 Favorite remove is idempotent when the row is already absent.
- [ ] 1.5 Member A cannot list or remove member B's favorites.
- [ ] 1.6 Hidden or missing games cannot be favorited and hidden favorited games are excluded from list results.
- [ ] 1.7 Favorites list returns current `Game.CompatibilityStatus` and sorts rows by title A-Z.
- [ ] 1.8 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 1.9 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

### Phase 2: Detail And Favorites UI

#### Automated

- [ ] 2.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 2.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual

- [ ] 2.3 Anonymous detail page still shows compatibility evidence without login.
- [ ] 2.4 Anonymous favorite CTA routes to `/login` with a local return URL for the current game detail page.
- [ ] 2.5 Signed-in detail page shows the correct add/remove favorite state.
- [ ] 2.6 Adding a favorite updates the detail page inline.
- [ ] 2.7 Removing a favorite updates the detail page inline.
- [ ] 2.8 `/favorites` shows only the signed-in member's visible favorite games in title order.
- [ ] 2.9 Removing from `/favorites` updates the list inline.
- [ ] 2.10 Signed-in nav shows `Favorites`; anonymous nav does not.
- [ ] 2.11 Favorites UI remains usable on narrow mobile widths without clipped text or overflowing buttons.

### Phase 3: Documentation And Final Manual Verification

#### Automated

- [ ] 3.1 All Phase 1 and Phase 2 automated checks pass.
- [ ] 3.2 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 3.3 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual

- [ ] 3.4 Full login-to-favorite smoke flow works using the development logging email sender.
- [ ] 3.5 Logout hides member favorites navigation and prevents direct `/favorites` access through the existing auth flow.
- [ ] 3.6 Existing lookup, browse, and detail flows still work for anonymous users.
- [ ] 3.7 No UI copy implies social features, profile management, or personalized compatibility.
