# Frame Brief: Production Catalog Interactivity

> Framing step before /10x-plan. This document captures what is *actually*
> at issue, separated from what was initially assumed.

## Reported Observation

On production deployment pagination doesn't work when browsing catalog and
search button refuses to give usable results.

## Initial Framing (preserved)

- **User's stated cause or approach**: No cause was stated; the request was observation-driven.
- **User's proposed direction**: Fix both production behaviors under one change.
- **Pre-dispatch narrowing**: Treat pagination and search as several distinct observations, then test whether evidence links them.

## Dimension Map

The observations could originate at any of these dimensions:

1. **Database read behavior** — offset paging or PostgreSQL title matching could return incorrect data.
2. **Component event handling** — the page-specific click and submit handlers could fail independently.
3. **Interactive Server bootstrap** — both components require a browser-side Blazor circuit before their handlers can execute.
4. **Production static-asset delivery** — the deployed image or endpoint mapping could fail to serve the bootstrap asset. ← confirmed shared boundary

## Hypothesis Investigation

| Hypothesis | Evidence | Verdict |
| --- | --- | --- |
| Database paging/search is defective | Paging uses ordered `Skip`/`Take`; search uses PostgreSQL `ILike`; PostgreSQL search tests exist. Production logs show successful catalog SQL and no search SQL invocation. (`LinuxGameCompat/Services/GameCompatibilityReadService.cs:17`, `LinuxGameCompat/Services/GameCompatibilityReadService.cs:40`) | NONE |
| Page handlers fail independently | Both handlers are straightforward and only call the read service. Neither can run without interactivity. (`LinuxGameCompat/Components/Pages/Home.razor:81`, `LinuxGameCompat/Components/Pages/Games.razor:85`) | WEAK |
| Interactive Server never starts | Both pages declare `InteractiveServer`; production HTML contains server-component markers, but the required bootstrap script returns HTTP 404. (`LinuxGameCompat/Components/Pages/Home.razor:2`, `LinuxGameCompat/Components/Pages/Games.razor:2`, `LinuxGameCompat/Components/App.razor:20`) | STRONG |
| Production fails to deliver the Blazor bootstrap asset | `https://linux-game-compat-production.up.railway.app/_framework/blazor.web.js` returned HTTP 404 on 2026-06-30. A local Release publish contains that file and its endpoint-manifest entry. The Dockerfile intends to copy the full publish directory. (`Dockerfile:9`, `Dockerfile:20`, `LinuxGameCompat/Program.cs:141`) | STRONG |

## Narrowing Signals

- The user identified pagination and search as separate observations rather than assuming one symptom.
- Both observations converge on the same missing interactivity boundary.
- Catalog prerendering and SQL reads succeed, so the database can populate the initial page.
- No search query reaches PostgreSQL when the production search control is used.
- Without Blazor bootstrap, pagination buttons are inert and the search form falls back to a plain GET that loses component state.

## Cross-System Convention

An Interactive Server Blazor page must load its framework bootstrap script and
establish a circuit before browser events can invoke component handlers. A 404
for `/_framework/blazor.web.js` violates that runtime contract. The local
Release artifact contains the expected asset, so the remaining distinction is
between deployed-image contents and production endpoint mapping.

## Reframed (or Confirmed) Problem Statement

> **The actual problem to plan around is**: The production deployment does not serve the Blazor browser bootstrap asset, so Interactive Server components remain prerendered HTML and cannot process catalog pagination or search events.

The two reported features are not supported as independent query defects.
They are distinct user-visible consequences of one failed production
interactivity boundary. Restoring and verifying that boundary should make both
handlers reachable; any remaining feature-specific defect can then be assessed
separately.

## Confidence

- **HIGH** — the live bootstrap URL returns 404, both symptoms require that
  bootstrap, database reads succeed, and a local Release publish contains the
  expected asset.

## What Changes for /10x-plan

Plan around diagnosing and restoring production delivery of the Blazor
bootstrap/static-asset contract, then verify both user flows end to end. Do not
start by redesigning pagination or search queries.

## References

- Source files: `LinuxGameCompat/Components/App.razor:20`, `LinuxGameCompat/Components/Pages/Home.razor:2`, `LinuxGameCompat/Components/Pages/Games.razor:2`, `LinuxGameCompat/Program.cs:141`, `Dockerfile:9`
- Production check: `https://linux-game-compat-production.up.railway.app/_framework/blazor.web.js` returned HTTP 404 on 2026-06-30
- Related research: no `context/changes/fix-production-problems/research.md` exists
- Investigation tasks: direct read-only investigation; no delegated tasks were created
