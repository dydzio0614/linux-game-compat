# Anonymous Compatibility Lookup — Plan Brief

> Full plan: `context/changes/anonymous-compatibility-lookup/plan.md`

## What & Why

Build the first anonymous lookup flow for the Linux compatibility app. Visitors will search for a game, open a detail page, and see source-linked compatibility evidence instead of manually comparing fragmented compatibility sites.

## Starting Point

F-01 already provides PostgreSQL persistence, source-backed evidence, and `IGameCompatibilityReadService` search/detail methods. The Blazor UI is still starter content and does not consume the compatibility read service.

## Desired End State

The root page is a search-first lookup UI. Search results link to `/games/{slug}`, where users see plain compatibility status labels, grouped evidence claims, source links, optional supporting summary text, and an explicit no-evidence state for `Unknown` games without claims.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Backend boundary | Use `IGameCompatibilityReadService` | Keeps hidden/no-evidence behavior centralized in the F-01 read service. |
| Search semantics | Treat `%`, `_`, and `\` literally | Public search should behave predictably for user-entered title text. |
| Home page | Search-first only | Matches the PRD focus on users arriving with a specific game. |
| Status labels | Plain labels | Aligns with the existing internal status contract without adding copy ambiguity. |
| Detail hierarchy | Evidence-first | Preserves the PRD guardrail that compatibility claims remain source-verifiable. |
| No-evidence state | Explicit uncertainty message | Avoids false certainty when no source-backed evidence has been curated. |
| Missing/hidden slugs | Same generic not-found state | Fits the current service contract and does not reveal hidden records. |
| Starter UI | Remove Counter/Weather public routes | Keeps the public app from looking like a template. |
| UI testing | Build/tests plus manual smoke | Matches current repo conventions without adding UI test tooling. |

## Scope

**In scope:**

- Literal title search hardening.
- Search-first home page.
- `/games/{slug}` detail page.
- Plain status labels.
- Source-linked evidence and source-reference rendering.
- Explicit no-evidence messaging.
- Counter/Weather starter UI cleanup.
- Automated backend tests plus manual browser smoke checks.

**Out of scope:**

- Auth, favorites, admin tooling, and account features.
- Browse page or default catalog listing.
- Live source API calls or broad crawling.
- GPT summary generation and background jobs.
- Database schema redesign.
- New UI test framework.

## Architecture / Approach

Keep EF Core and PostgreSQL behind the existing read-service layer. Harden the service's title search with escaped `ILIKE`, then build Blazor interactive server pages that consume read models for lookup and detail display. The UI renders evidence claims and source references directly so source traceability remains visible.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Search Contract Hardening | Literal public search and regression tests | Incorrect escaping could break normal title search. |
| 2. Lookup Home Page | Interactive search-first root page | UI state can become confusing around empty/loading/results states. |
| 3. Game Detail Evidence Page | Evidence-first `/games/{slug}` page | Summary/no-evidence copy could imply more certainty than sources support. |
| 4. Starter UI Cleanup And Handoff | Template UI removed and verification complete | Removing starter routes requires smoke checks for navigation/not-found behavior. |

**Prerequisites:** F-01 local database/migrations are available; existing read-service tests pass.
**Estimated effort:** About 1-2 focused implementation sessions across 4 phases.

## Open Risks & Assumptions

- No UI test framework exists; manual browser smoke testing covers page rendering for this slice.
- The existing seed catalog is small, so the home page intentionally avoids a default browse list.
- Stored summaries are optional supporting text only; generated-summary work remains future scope.
- No schema migration is expected.

## Success Criteria (Summary)

- Anonymous users can search by title, choose a result, and open a useful game detail page.
- Detail pages show source-linked evidence and clear no-evidence uncertainty.
- Starter Counter/Weather UI is gone from the public app surface.
- `dotnet build LinuxGameCompat.sln --no-restore` and `dotnet test LinuxGameCompat.sln --no-restore` pass.
