# Modernize Visual Presentation — Plan Brief

> Full plan: `context/changes/modernize-visual-presentation/plan.md`

## What & Why

Modernize the existing Blazor UI into a polished, trustworthy evidence-dashboard experience. The roadmap calls for replacing the current Bootstrap-default-like presentation with stronger layout, typography, spacing, color, status treatments, and evidence-focused page composition.

## Starting Point

The app already has the core MVP pages: lookup, browse, detail, favorites, and login. Styling is centralized in `app.css`, but the shell and repeated UI surfaces still look close to the default Blazor/Bootstrap starter presentation.

## Desired End State

Users see a more cohesive, modern interface across the main pages without any behavior or backend changes. Status is easier to scan, evidence feels authoritative, generated summaries remain properly framed, and the app works cleanly on desktop and mobile.

## Key Decisions Made

| Decision | Choice | Why |
|---|---|---|
| Visual direction | Evidence dashboard | Fits the product's source-backed decision-support purpose. |
| Scope | Core visible pages only | Visual quality is the goal; future component reuse is not important right now. |
| Detail composition | Summary then evidence | Preserves current scanning flow while strengthening evidence hierarchy. |
| Layout shell | Modernize existing sidebar | Lowest behavioral risk and keeps current navigation structure. |
| Status treatment | Semantic badges | Improves list/detail scanning while preserving text labels. |
| Branding depth | Subtle static motif | Adds identity without image-heavy scope or distraction. |
| Verification | Manual responsive pass | Fits current tooling and catches visual issues automated tests do not cover. |

## Scope

**In scope:**

- Shared CSS visual foundation and tokens.
- Modernized sidebar/top-row shell.
- Lookup, browse, favorites, login, and detail page presentation.
- Semantic compatibility status badges.
- Summary, evidence, source, empty, loading, pager, and action-state styling.
- Desktop and mobile manual visual verification.

**Out of scope:**

- Frontend rewrite, new framework, backend changes, schema changes, route changes, auth changes, formal component library, screenshot-test tooling, rich imagery, generated bitmap assets, or new product behavior.

## Architecture / Approach

Keep the existing Blazor Server and Bootstrap-backed app. Make app-owned CSS classes carry the product design, update only minimal Razor markup where needed for semantic status badge classes or better structure, and keep all service/data behavior unchanged.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Visual foundation and shell | Tokens, base styles, motif, sidebar/top-row polish | Starter-shell behavior regresses on mobile |
| 2. Lookup, browse, favorites, and login surfaces | Polished repeated forms, rows, empty/loading states, pagers, actions | Long titles/actions overflow on narrow screens |
| 3. Game detail evidence composition | Stronger status, summary, evidence, and source hierarchy | Generated summary appears too authoritative |
| 4. Responsive and regression verification | Build/test plus manual desktop/mobile pass | Visual issues missed without browser review |

**Prerequisites:** Existing seeded/local data sufficient to view populated lookup, browse, detail, and favorites states.

**Estimated effort:** About 2-3 implementation sessions across 4 gated phases.

## Open Risks & Assumptions

- Existing Bootstrap remains available; the app-specific CSS should reduce the default look without removing Bootstrap.
- No new automated visual testing is planned, so manual browser review is required.
- The subtle motif should be local and lightweight.
- Status badges must remain understandable without color alone.

## Success Criteria (Summary)

- Main pages look cohesive, modern, and evidence-focused on desktop and mobile.
- Existing behavior, routes, auth, source evidence, and generated-summary trust labels are preserved.
- `dotnet build LinuxGameCompat.sln --no-restore` and `dotnet test LinuxGameCompat.sln --no-restore` pass.
