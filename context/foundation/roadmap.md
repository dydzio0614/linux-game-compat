---
project: "Linux Compatibility Aggregator"
version: 1
status: draft
created: 2026-05-26
updated: 2026-05-26
prd_version: 1
main_goal: speed
top_blocker: capacity
---

# Roadmap: Linux Compatibility Aggregator

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Vision recap

New people switching to Linux face decision paralysis because compatibility evidence is fragmented across partial sources and comment threads. The product value is source-linked synthesis: a compact current status plus a breakdown of reasoning, caveats, and common workarounds. The MVP should keep source coverage limited while making unsupported claims visibly uncertain.

## North star

**S-01: Anonymous user can check game compatibility with source-linked evidence** - this is the north star, meaning the smallest end-to-end slice whose delivery proves the product works: search, result, detail, compatibility reasoning, caveats, workarounds, and source links.

## At a glance

| ID | Change ID | Outcome (user can ...) | Prerequisites | PRD refs | Status |
|---|---|---|---|---|---|
| F-01 | minimal-evidence-baseline | (foundation) initial source-backed game catalog and compatibility evidence path exists | - | NFR source links, Business Logic | ready |
| S-01 | anonymous-compatibility-lookup | user can search for a game, open details, and see source-linked compatibility reasoning | F-01 | US-01, FR-001, FR-002, FR-003, FR-004 | proposed |
| F-02 | passwordless-member-access | (foundation) passwordless member identity is available for favorites | S-01 | Access Control, FR-006 | proposed |
| S-02 | member-favorites-tracking | logged-in member can save games and view favorites with current status | S-01, F-02 | US-02, FR-007, FR-008 | proposed |
| S-03 | browse-available-games | user can browse available games without a search phrase | F-01 | FR-005 | proposed |

## Streams

Navigation aid - groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme | Chain | Note |
|---|---|---|---|
| A | Lookup and member path | `F-01` -> `S-01` -> `F-02` -> `S-02` | Puts the must-have lookup path first, then adds the member feature that depends on it. |
| B | Catalog browsing | `S-03` | Uses `F-01` from Stream A, but stays behind the must-have launch path because browsing is nice-to-have. |

## Baseline

What's already in place in the codebase as of `2026-05-26` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** present - .NET 10 Blazor/Razor Components with Blazor routing and starter pages.
- **Backend / API:** partial - ASP.NET Core host exists, but no API endpoints/request handlers beyond Razor pages.
- **Data:** absent - no DB package, connection string, schema, migrations, seeds, or data-layer code.
- **Auth:** absent - no auth packages, provider integration, token/session handling, or route auth middleware.
- **Deploy / infra:** partial - Docker/Railway deployment path exists; CI/CD and infra-as-code are absent.
- **Observability:** partial - built-in logging config and exception/status pages exist; no metrics or external error tracking.

## Foundations

### F-01: Minimal evidence baseline

- **Outcome:** (foundation) initial source-backed game catalog and compatibility evidence path exists for the first lookup flow.
- **Change ID:** minimal-evidence-baseline
- **PRD refs:** NFR source links, Business Logic
- **Unlocks:** S-01, S-03
- **Prerequisites:** -
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:** -
- **Risk:** If this foundation tries to solve broad crawling or a complete game database, it will consume the capacity needed for the first usable lookup path.
- **Status:** ready

### F-02: Passwordless member access

- **Outcome:** (foundation) passwordless member identity is available so favorites can belong to a logged-in member.
- **Change ID:** passwordless-member-access
- **PRD refs:** Access Control, FR-006
- **Unlocks:** S-02
- **Prerequisites:** S-01
- **Parallel with:** S-03
- **Blockers:** -
- **Unknowns:** -
- **Risk:** Auth is necessary for favorites, but sequencing it after anonymous lookup keeps the first launch path focused on the core compatibility question.
- **Status:** proposed

## Slices

### S-01: Anonymous compatibility lookup

- **Outcome:** user can search for a game, choose a result, open details, and see current compatibility status with source-linked reasoning, caveats, and common workarounds.
- **Change ID:** anonymous-compatibility-lookup
- **PRD refs:** US-01, FR-001, FR-002, FR-003, FR-004
- **Prerequisites:** F-01
- **Parallel with:** -
- **Blockers:** -
- **Unknowns:**
  - What exact compatibility status vocabulary should the UI expose? - Owner: user. Block: no.
- **Risk:** This is the first user-visible proof of value; if the source links and caveats are weak, the app becomes only another compact status list.
- **Status:** proposed

### S-02: Member favorites tracking

- **Outcome:** logged-in member can save games from detail pages and view a personal favorites list with current compatibility status for each saved game.
- **Change ID:** member-favorites-tracking
- **PRD refs:** US-02, FR-007, FR-008
- **Prerequisites:** S-01, F-02
- **Parallel with:** S-03
- **Blockers:** -
- **Unknowns:**
  - What exact compatibility status vocabulary should the UI expose? - Owner: user. Block: no.
- **Risk:** Favorites only become useful after game status and detail pages exist, so building this before S-01 would front-load account work without validating the main lookup flow.
- **Status:** proposed

### S-03: Browse available games

- **Outcome:** user can browse the available game catalog without submitting a search phrase.
- **Change ID:** browse-available-games
- **PRD refs:** FR-005
- **Prerequisites:** F-01
- **Parallel with:** F-02, S-02
- **Blockers:** -
- **Unknowns:**
  - What exact compatibility status vocabulary should the UI expose? - Owner: user. Block: no.
- **Risk:** Browsing is useful but not required for the MVP's primary lookup behavior, so speed favors delaying it behind the must-have search/detail path.
- **Status:** proposed

## Backlog Handoff

| Roadmap ID | Change ID | Suggested issue title | Ready for `/10x-plan` | Notes |
|---|---|---|---|---|
| F-01 | minimal-evidence-baseline | Establish minimal source-backed evidence baseline | yes | Run `/10x-plan minimal-evidence-baseline` |
| S-01 | anonymous-compatibility-lookup | Build anonymous compatibility lookup with evidence links | no | Depends on F-01 |
| F-02 | passwordless-member-access | Add passwordless member access for favorites | no | Depends on S-01 |
| S-02 | member-favorites-tracking | Add member favorites tracking | no | Depends on S-01 and F-02 |
| S-03 | browse-available-games | Add browseable available-games list | no | Nice-to-have; depends on F-01 |

## Open Roadmap Questions

1. **What exact compatibility status vocabulary should the UI expose?** - Owner: user. Block: S-01, S-02, S-03.

## Parked

- **Social features or user-made compatibility guides** - Why parked: PRD Non-Goals; the MVP summarizes existing external evidence rather than hosting community content.
- **Broad extra-source crawling and broad community-thread browsing** - Why parked: PRD Non-Goals; initial source coverage is intentionally limited to keep the MVP shippable.
- **Personalized research by exact hardware, distribution, settings, or game library** - Why parked: PRD Non-Goals; summaries describe general compatibility evidence.
- **Complete game database or optimized per-game polling at MVP scale** - Why parked: PRD Non-Goals; the initial catalog can be limited to games available through the first supported sources.
- **Search suggestions as the user types** - Why parked: PRD Non-Goals; MVP search submits a phrase and then shows matching results.

## Done

<!-- Empty on first generation. `/10x-archive` appends entries here and flips matching roadmap items to `done`. -->
