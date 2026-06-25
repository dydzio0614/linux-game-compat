# Modernize Visual Presentation Implementation Plan

## Overview

Modernize the existing Blazor Server UI into a polished evidence-dashboard experience while preserving current routes, services, data contracts, auth behavior, and Bootstrap-backed stack. The work focuses on visible quality gains across the app: layout, typography, spacing, color, status badges, evidence hierarchy, empty/loading states, and responsive behavior.

## Current State Analysis

The app already has the core user-facing pages from earlier roadmap slices: lookup, browse, game detail, favorites, and login. The UI is functional, but much of the shell still resembles the default Blazor sidebar template and the page styling is light Bootstrap-adjacent polish rather than a cohesive product presentation.

The CSS is currently centralized enough for a focused visual pass. Shared classes already exist for lookup/detail pages, headers, result rows, status labels, empty states, summary notes, evidence lists, source lists, favorite actions, and mobile stacking.

## Desired End State

Users see a noticeably more polished, modern interface that feels like a trustworthy evidence dashboard for Linux game compatibility. The lookup, browse, favorites, login, and detail pages share a coherent visual language; status and evidence are easier to scan; generated summaries remain useful without overpowering source-backed claims.

The implementation is complete when the app builds and tests pass, the main pages are manually verified at desktop and mobile widths, and existing behavior is preserved.

### Key Discoveries:

- The roadmap scopes S-06 to visible quality gains in the existing Blazor frontend, with no frontend stack replacement.
- `LinuxGameCompat/wwwroot/app.css` already owns the repeated page, row, badge, summary, evidence, and responsive classes used across the main pages.
- `LinuxGameCompat/Components/Layout/MainLayout.razor` and `LinuxGameCompat/Components/Layout/NavMenu.razor` still follow the default sidebar/top-row shell closely.
- `LinuxGameCompat/Components/Pages/GameDetail.razor` already renders generated summary first, then source-backed evidence and source references; this order should remain.

## What We're NOT Doing

- No frontend rewrite, new SPA framework, or separate frontend project.
- No backend, database, service contract, route, auth, or summary-generation behavior changes.
- No formal component library or broad design-system documentation.
- No screenshot-test infrastructure in this slice.
- No rich imagery or generated bitmap hero assets.
- No evidence ingestion, source crawling, or product behavior changes.

## Implementation Approach

Keep the current Blazor and Bootstrap foundation, but make the app-specific classes carry the visual design. Add a small set of CSS custom properties for colors, spacing, surfaces, focus, and shadows; modernize the existing sidebar shell; then apply the same treatment to repeated page surfaces and the game detail evidence composition.

The selected direction is a quiet, trustworthy evidence dashboard with a subtle static Linux/game/evidence-themed motif. The motif must be local, CSS-based or a small local asset, and secondary to the product content.

## Phase 1: Visual Foundation and Shell

### Overview

Create the shared visual foundation and modernize the app shell without changing navigation behavior.

### Changes Required:

#### 1. Shared Visual Tokens and Base Styles

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Establish a cohesive visual language for the full app using app-owned CSS rather than scattered Bootstrap defaults.

**Contract**: Add CSS custom properties for background, text, muted text, borders, surfaces, elevated surfaces, primary action color, semantic status colors, focus ring, radii, and shadows. Update body, links, buttons, forms, page containers, headings, and focus states to use those tokens. Keep cards at 8px radius or less.

#### 2. Subtle Static Motif

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Add light brand character without introducing image-heavy or stock-like presentation.

**Contract**: Add a subtle, static Linux/game/evidence-themed motif through CSS or a small local asset. It must not require external network loading, obscure content, reduce contrast, or dominate the first viewport.

#### 3. Layout Shell

**File**: `LinuxGameCompat/Components/Layout/MainLayout.razor`, `LinuxGameCompat/Components/Layout/MainLayout.razor.css`

**Intent**: Make the page shell feel deliberate and product-specific while preserving the existing sidebar/top-row structure.

**Contract**: Keep `@Body`, the sidebar, top row, and sticky desktop behavior. Improve page background, content width/spacing, top-row presentation, and mobile spacing. Remove the starter-template feel without changing route or auth behavior.

#### 4. Navigation

**File**: `LinuxGameCompat/Components/Layout/NavMenu.razor`, `LinuxGameCompat/Components/Layout/NavMenu.razor.css`

**Intent**: Modernize the desktop sidebar and mobile collapsible navigation.

**Contract**: Preserve the existing links, authorization branches, logout form, antiforgery token, and checkbox-driven mobile collapse. Improve brand treatment, active/hover/focus states, member email truncation, icon alignment, and touch targets.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.

#### Manual Verification:

- Desktop sidebar, active nav state, top row, and content area look cohesive.
- Mobile navigation remains collapsible and usable.
- Keyboard focus is visible on navigation, buttons, links, and inputs.

**Implementation Note**: After completing this phase and automated verification passes, pause for manual confirmation before proceeding.

---

## Phase 2: Lookup, Browse, Favorites, and Login Surfaces

### Overview

Apply the new visual foundation to the repeated public and member-facing page surfaces.

### Changes Required:

#### 1. Lookup Page

**File**: `LinuxGameCompat/Components/Pages/Home.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make the primary search flow look like the app's main experience rather than a basic form.

**Contract**: Preserve the existing search binding, submit behavior, loading label, empty state, result links, and ARIA live region. Improve page header hierarchy, form layout, result list presentation, and mobile stacking.

#### 2. Browse Page

**File**: `LinuxGameCompat/Components/Pages/Games.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make catalog browsing consistent with search results while keeping pagination simple.

**Contract**: Preserve `PageSize`, loading behavior, empty state, result rows, and previous/next paging logic. Improve loading presentation, result spacing, pager treatment, disabled states, and narrow-screen layout.

#### 3. Favorites Page

**File**: `LinuxGameCompat/Components/Pages/Favorites.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make saved games feel like a first-class member surface.

**Contract**: Preserve authorization, favorite removal behavior, feedback messages, empty-state actions, and detail links. Improve row actions, remove/loading state presentation, empty-state action layout, and long-title handling.

#### 4. Login Page

**File**: `LinuxGameCompat/Components/Pages/Login.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Bring passwordless member access into the same visual language as the rest of the app.

**Contract**: Preserve form action, hidden return URL, query-state rendering, email autocomplete/required attributes, and all existing user-facing states. Improve form layout and success/error state treatment.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual Verification:

- Search page looks polished before search, during search, with no results, and with populated results.
- Browse page loading, empty, populated, and pager states are clear.
- Favorites empty, populated, feedback, and removing states are readable and stable.
- Login normal, sent, failed, and request-failed states are visually consistent.

**Implementation Note**: After automated verification and page review pass, pause for manual confirmation before proceeding.

---

## Phase 3: Game Detail Evidence Composition

### Overview

Improve the most important evidence page while preserving the current content order: generated summary first, source-backed evidence immediately below, then source references.

### Changes Required:

#### 1. Detail Header and Favorite Area

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make the game title, public status, Steam metadata, and favorite action easier to scan.

**Contract**: Preserve loading/not-found behavior, `LoginReturnUrl`, favorite state loading, add/remove favorite behavior, feedback messages, and existing error strings. Improve header composition, metadata spacing, and favorite action layout.

#### 2. Semantic Status Badges

**File**: `LinuxGameCompat/Components/CompatibilityStatusLabels.cs`, `LinuxGameCompat/Components/Pages/Home.razor`, `LinuxGameCompat/Components/Pages/Games.razor`, `LinuxGameCompat/Components/Pages/Favorites.razor`, `LinuxGameCompat/Components/Pages/GameDetail.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make compatibility status faster to scan without relying on color alone.

**Contract**: Add one shared helper next to the existing public label mapping, for example `CompatibilityStatusLabels.ToCssClass(CompatibilityStatus status)`, and use it from Home, Games, Favorites, and GameDetail instead of duplicating status switch logic in each Razor page. Add status-specific CSS classes for Playable, Playable with caveats, Unsupported, and Unknown. Badges must still render the existing public text label. Use accessible contrast and preserve wrapping behavior on narrow screens.

#### 3. Generated Summary Panel

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make generated summaries useful and well-framed without implying they replace source evidence.

**Contract**: Preserve `ShouldShowSummary`, outdated warning, status disagreement advisory, AI fallback advisory, generated date, and temporary-unavailable text. Restyle summary, warning, and advisory treatments so trust state is clear.

#### 4. Evidence and Source Sections

**File**: `LinuxGameCompat/Components/Pages/GameDetail.razor`, `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make raw claims and source references feel like the authoritative audit trail.

**Contract**: Preserve evidence grouping, claim ordering, source link logic, external-link attributes, empty evidence states, and source ordering. Improve claim cards, metadata, section hierarchy, source links, and long claim text behavior.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual Verification:

- Detail page loading, not-found, normal, no-evidence, summary, stale summary, disagreement, and AI-fallback states remain understandable.
- Raw evidence and source links are clearly visible and authoritative.
- Semantic status badges are readable for every compatibility status.
- Favorite actions remain usable and stable on desktop and mobile.

**Implementation Note**: After automated verification and detail-page review pass, pause for manual confirmation before proceeding.

---

## Phase 4: Responsive and Regression Verification

### Overview

Verify the UI change across the app's key surfaces and fix visual regressions within the agreed scope.

### Changes Required:

#### 1. Automated Verification

**File**: Repository root

**Intent**: Confirm the visual-only change does not break build or existing behavior.

**Contract**: Run `dotnet build LinuxGameCompat.sln --no-restore` and `dotnet test LinuxGameCompat.sln --no-restore`.

#### 2. Manual Responsive Review

**File**: Running Blazor app

**Intent**: Catch visual issues that existing automated tests do not cover.

**Contract**: Review `/`, `/games`, `/games/{slug}`, `/favorites`, and `/login` at desktop and mobile widths. Use seeded data or existing local data to cover populated states. Check for text overlap, horizontal scrolling, clipped actions, unreadable contrast, missing focus states, and awkward evidence/source wrapping. Pay specific attention to shared selectors whose changes affect multiple states at once: `.empty-state`, `.result-meta`, `.lead`, `a`, `.btn-primary`, and `.btn:focus`.

#### 3. Final Scope Pass

**File**: Modified Razor/CSS files

**Intent**: Keep the implementation aligned with the MVP visual-polish goal.

**Contract**: Confirm the change did not introduce backend behavior changes, new infrastructure, unnecessary component abstractions, external assets, or broad design-system work.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual Verification:

- No incoherent overlap, clipping, or horizontal scrolling on key pages at mobile widths.
- Desktop pages have a cohesive visual hierarchy and readable evidence layout.
- Navigation, forms, buttons, links, disabled states, and focus states remain usable.
- The app feels visually modern without changing product behavior.

## Testing Strategy

### Unit Tests:

- No new unit tests are required unless implementation accidentally touches behavior. Existing tests should remain unchanged and green.

### Integration Tests:

- No new integration tests are required because this slice does not change services, EF schema, auth, summary generation, or read models.

### Manual Testing Steps:

1. Run the app locally and open `/`.
2. Verify lookup initial, searching, empty, and populated result states.
3. Open `/games` and verify loading, populated list, and pager enabled/disabled states.
4. Open at least one `/games/{slug}` detail page and verify summary, evidence groups, source references, status badge, and favorite area.
5. Open `/favorites` as an authenticated member and verify empty/populated/removing states where possible.
6. Open `/login?sent=1`, `/login?failed=1`, and `/login?requestFailed=1`.
7. Repeat core checks at mobile and desktop widths.

## Performance Considerations

This is a CSS/Razor presentation pass. Avoid external fonts, remote images, heavy JavaScript, and large assets. The subtle motif must be lightweight and local. Prefer CSS changes and existing markup over adding runtime dependencies.

## Migration Notes

No database migration is required.

## References

- Roadmap: `context/foundation/roadmap.md` S-06
- Current app CSS: `LinuxGameCompat/wwwroot/app.css`
- Shell: `LinuxGameCompat/Components/Layout/MainLayout.razor`, `LinuxGameCompat/Components/Layout/NavMenu.razor`
- Core pages: `LinuxGameCompat/Components/Pages/Home.razor`, `LinuxGameCompat/Components/Pages/Games.razor`, `LinuxGameCompat/Components/Pages/GameDetail.razor`, `LinuxGameCompat/Components/Pages/Favorites.razor`, `LinuxGameCompat/Components/Pages/Login.razor`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Visual Foundation and Shell

#### Automated

- [ ] 1.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.

#### Manual

- [ ] 1.2 Desktop sidebar, active nav state, top row, and content area look cohesive.
- [ ] 1.3 Mobile navigation remains collapsible and usable.
- [ ] 1.4 Keyboard focus is visible on navigation, buttons, links, and inputs.

### Phase 2: Lookup, Browse, Favorites, and Login Surfaces

#### Automated

- [ ] 2.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- [ ] 2.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual

- [ ] 2.3 Search page looks polished before search, during search, with no results, and with populated results.
- [ ] 2.4 Browse page loading, empty, populated, and pager states are clear.
- [ ] 2.5 Favorites empty, populated, feedback, and removing states are readable and stable.
- [ ] 2.6 Login normal, sent, failed, and request-failed states are visually consistent.

### Phase 3: Game Detail Evidence Composition

#### Automated

- [ ] 3.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- [ ] 3.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual

- [ ] 3.3 Detail page loading, not-found, normal, no-evidence, summary, stale summary, disagreement, and AI-fallback states remain understandable.
- [ ] 3.4 Raw evidence and source links are clearly visible and authoritative.
- [ ] 3.5 Semantic status badges are readable for every compatibility status.
- [ ] 3.6 Favorite actions remain usable and stable on desktop and mobile.

### Phase 4: Responsive and Regression Verification

#### Automated

- [ ] 4.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`.
- [ ] 4.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`.

#### Manual

- [ ] 4.3 No incoherent overlap, clipping, or horizontal scrolling on key pages at mobile widths.
- [ ] 4.4 Desktop pages have a cohesive visual hierarchy and readable evidence layout.
- [ ] 4.5 Navigation, forms, buttons, links, disabled states, and focus states remain usable.
- [ ] 4.6 The app feels visually modern without changing product behavior.
