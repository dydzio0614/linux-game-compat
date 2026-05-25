---
project: "Linux Compatibility Aggregator"
context_type: greenfield
created: 2026-05-21
updated: 2026-05-21
product_type: web-app
target_scale:
  users: medium
  qps: low
  data_volume: small
timeline_budget:
  mvp_weeks: 3
  hard_deadline: null
  after_hours_only: true
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  gray_areas_resolved:
    - topic: "context type"
      decision: "Greenfield project."
    - topic: "primary persona"
      decision: "New Linux switcher."
    - topic: "pain category"
      decision: "Decision paralysis caused by fragmented compatibility evidence."
    - topic: "access model"
      decision: "Anonymous users can browse/search/detail; passwordless members can save and track favorites."
    - topic: "search interaction"
      decision: "Search is submit-to-results for MVP; suggestions as-you-type are out of scope."
    - topic: "MVP timeline"
      decision: "Three after-hours weeks, no hard deadline."
    - topic: "FR priority"
      decision: "Search/detail/summary/auth/favorites are must-have; browsing all games is nice-to-have."
    - topic: "auth recovery"
      decision: "Passwordless login has no account recovery in MVP."
    - topic: "traceability"
      decision: "Every compatibility status, caveat, and workaround claim on a game detail page must include source links."
    - topic: "scale behavior"
      decision: "At higher scale, refresh and summary quality should prioritize popular searched or favorited games."
    - topic: "MVP non-goals"
      decision: "No social guides, advanced integrations, personalized research, or complete game database."
  frs_drafted: 8
  quality_check_status: accepted
---

## Vision & Problem Statement

New people switching to Linux face decision paralysis when checking whether specific games will work. They need to know which partial compatibility sites exist, compare fragmented data, and read comments for common workarounds before they can decide whether a game is playable enough for them.

The product insight is that existing compatibility sources are useful but incomplete on their own. The value is synthesis: aggregating multiple partial sources and surfacing a quick, source-linked compatibility summary with important caveats and commonly mentioned workarounds.

At larger scale, the synthesis rule should prioritize refresh and summary quality for games that many users search for or favorite.

## User & Persona

Primary persona: a new Linux switcher who wants to know whether particular games will work before or soon after switching. They reach for the product when deciding whether a game is playable on Linux without manually researching several compatibility websites and comment threads.

Secondary users may include existing Linux gamers who gain interest in particular games or want a convenient place to track compatibility changes, but the MVP is shaped around the new switcher.

## Success Criteria

### Primary

- An anonymous user can submit a game title search, see matching games with a compact current compatibility status, open a game detail page, and reach a source-linked compatibility breakdown with reasoning, caveats, and common workarounds.

### Secondary

- A logged-in member can save games to favorites and view a personal list that shows current compatibility status for those games.

### Guardrails

- Summaries must expose source links so compatibility status, caveats, and workaround claims can be verified.
- The MVP should not create false certainty when source evidence is partial or incomplete.

## User Stories

### US-01: Anonymous user checks game compatibility

- **Given** an anonymous visitor wants to check whether a specific game works on Linux
- **When** they submit a title phrase, choose a matching game, and open its detail page
- **Then** they see the current compatibility status, a compatibility breakdown, caveats or common workarounds when present, and links to supporting sources

#### Acceptance Criteria

- Search results show matching game titles from the available catalog.
- Search results show a compact current compatibility status where available.
- The game detail page includes source links for compatibility status, caveats, and workaround claims.
- The detail page does not present unsupported claims as certain when source evidence is incomplete.

### US-02: Member tracks favorite games

- **Given** a logged-in member is viewing a game detail page
- **When** they add the game to favorites
- **Then** the game appears in their personal list with its current compatibility status

#### Acceptance Criteria

- A logged-in member can add a game to favorites.
- A logged-in member can open a personal favorite-games page.
- The personal list shows the current compatibility status for each saved game.

## Functional Requirements

### Public lookup

- FR-001: Anonymous visitor can search available games by title phrase. Priority: must-have
  > Socratic: Counter-argument considered: limited initial source coverage may make search feel weak. Resolution: kept; search is the primary lookup path, and initial coverage limitations are accepted for MVP.
- FR-002: Anonymous visitor can open a specific game detail page. Priority: must-have
  > Socratic: Counter-argument considered: no counter-argument; the detail page is necessary because it carries the compatibility breakdown and source links.
- FR-003: Anonymous visitor can see a current compatibility status for a game. Priority: must-have
  > Socratic: Counter-argument considered: compact status can duplicate source sites if it lacks explanation. Resolution: kept; compact status points users to the detail page where evidence and caveats are shown.
- FR-004: Anonymous visitor can see a detailed compatibility breakdown with likely compatibility state, reasoning, caveats, common workarounds, and source links. Priority: must-have
  > Socratic: Counter-argument considered: no counter-argument; the detailed source-linked summary is the core product value.
- FR-005: Anonymous visitor can browse a list of available games for compatibility. Priority: nice-to-have
  > Socratic: Counter-argument considered: search may be enough for MVP users who arrive with a specific game in mind. Resolution: demoted to nice-to-have.

### Member personalization

- FR-006: Visitor can log in passwordlessly to access member features. Priority: must-have
  > Socratic: Counter-argument considered: no recovery clarity. Resolution: kept with no account recovery in MVP as an intentional limitation.
- FR-007: Logged-in member can add games to favorites. Priority: must-have
  > Socratic: Counter-argument considered: favorites are less valuable unless they show change or status value. Resolution: kept; favorites support a personal list with current compatibility status.
- FR-008: Logged-in member can view a personal list of favorite games with current compatibility status. Priority: must-have
  > Socratic: Counter-argument considered: users could use browser bookmarks instead. Resolution: kept; the personal list shows current compatibility status, not just saved links.

## Non-Functional Requirements

- Every compatibility status, caveat, and workaround claim on a game detail page includes links to the source evidence behind that claim.
- After submitting a search, a user reaches a useful game detail page with current generated or available summary within 10 seconds under normal conditions.

## Business Logic

The app aggregates incomplete Linux game compatibility evidence from multiple sources and uses that evidence to summarize the likely compatibility state, reasoning, source-backed caveats, and commonly mentioned workarounds so users can decide faster.

Inputs are compatibility evidence from the initial supported compatibility sources, plus source comments or notes that mention important caveats and common workarounds. The product output is a user-facing compatibility summary for a specific game, with a compact current status and detailed source-linked reasoning on the game detail page.

The user encounters this rule after searching for a game and opening its detail page. The rule should save the user from manually comparing multiple partial sources and reading comment threads to discover whether a game works, why it may fail, and what common workaround might be required.

## Access Control

Anonymous visitors can search for games, view search results, and open game detail pages.

Logged-in members can save games to favorites and view a personal list of favorite games with current compatibility status.

Login is passwordless. The MVP intentionally has no account recovery; losing access to the login identity means losing access to the member's saved favorites.

There is no admin role in the MVP access model.

## Non-Goals

- No social features or user-made compatibility guides; the MVP summarizes existing external evidence rather than hosting community content.
- No advanced integrations such as broad community-thread browsing or broad extra-source crawling; initial source coverage is intentionally limited.
- No personalized research based on a user's exact hardware, distribution, settings, or game library; summaries describe general compatibility evidence.
- No complete game database or optimized per-game polling at MVP scale; the initial catalog can be limited to games available through the first supported sources.
- No search suggestions as the user types; MVP search submits a phrase and then shows matching results.

## Open Questions

1. **What exact compatibility status vocabulary should the UI expose?** — Owner: user. The MVP currently assumes a compact current status in lists and richer caveats on detail pages; the exact labels can be resolved during product/UI design.

## Forward: source-service candidates

- Initial supported compatibility sources discussed during shaping: ProtonDB and AreWeAntiCheatYet.
- Broad community-thread browsing, including Reddit, is intentionally outside MVP scope and should be considered only by downstream source/integration planning.

## Quality cross-check

- Access Control: present.
- Business Logic: present as a one-sentence domain rule.
- Project artifacts: present with finalized checkpoint.
- Timeline-cost ack: present; MVP is scoped to 3 after-hours weeks.
- Non-Goals: present.
- Preserved behavior: n/a for greenfield.
