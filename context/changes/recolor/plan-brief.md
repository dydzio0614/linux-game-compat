# Recolor Blue Elements to Red - Plan Brief

> Full plan: `context/changes/recolor/plan.md`

## What & Why

Recolor app-owned blue visual accents to accessible crimson shades. The goal is a coherent red-accented UI without changing behavior, data, authentication, favorites, or compatibility evidence flows.

## Starting Point

The Blazor app loads Bootstrap first and `app.css` afterward, so app CSS can override Bootstrap primary styling. Blue currently appears in global app CSS, the sidebar gradient, reconnect modal styling, and Razor pages that use Bootstrap `btn-primary` / `btn-outline-primary`.

## Desired End State

Links, primary buttons, outline-primary buttons, focus rings, compatibility status chips, summary advisory blocks, sidebar background, and reconnect modal accents render in a restrained crimson palette. Warning, validation, success, and existing error-boundary styling keep their semantic colors.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Palette | Accessible crimson | Keeps contrast and avoids making every normal action feel destructive. |
| Scope | All app-owned blue accents | Delivers a coherent visible recolor across normal and reconnect states. |
| Semantic colors | Preserve state colors | Avoids reducing warning/success/validation clarity. |
| Bootstrap handling | Override in `app.css` | Covers existing Razor class usage without vendored Bootstrap edits or markup churn. |
| Verification | Build/test plus CSS review | Matches the risk profile of a small MVP styling change without adding visual test tooling. |

## Scope

**In scope:**

- App-owned global link, button, focus, status chip, and advisory styling.
- Bootstrap primary and outline-primary behavior as rendered by current pages.
- Sidebar background gradient.
- Reconnect modal button and animation accent colors.

**Out of scope:**

- Vendored Bootstrap files.
- Razor behavior or class rewrites unless CSS cannot cover a case.
- Database, service, auth, favorites, summary generation, or migration changes.
- Screenshot automation or browser test harness setup.

## Architecture / Approach

Use `app.css` as the theme override layer because it loads after Bootstrap. Replace hard-coded blue accents in global and isolated component CSS, while leaving Razor markup and semantic state colors intact.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Theme Accent Recolor | Crimson palette applied to app-owned blue accents | Missing Bootstrap outline-primary behavior or over-recoloring semantic states |
| 2. Build Verification | Build/test outcome recorded | Testcontainers may require Docker and fail for environmental reasons |

**Prerequisites:** .NET 10 SDK; Docker only if running the full Testcontainers-backed suite.
**Estimated effort:** One short implementation session plus verification.

## Open Risks & Assumptions

- The exact red shades are implementation choices within the accessible crimson direction.
- Static CSS review is the accepted visual verification level; no screenshot automation is expected.
- Vendored Bootstrap blue values remain present under `wwwroot/lib` and should be ignored.

## Success Criteria (Summary)

- App-owned blue accent colors are replaced with crimson equivalents.
- Existing semantic warning, validation, success, and error colors remain meaningful.
- `dotnet build LinuxGameCompat.sln` and, when prerequisites allow, `dotnet test LinuxGameCompat.sln` complete successfully or limitations are documented.
