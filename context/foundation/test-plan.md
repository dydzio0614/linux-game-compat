# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top
> (§1-§5); cookbook patterns at the bottom (§6) fill in as phases ship.
> Read before writing any new test.
>
> Refresh: re-run `/10x-test-plan --refresh` when stale (see §8).
>
> Last updated: 2026-06-03

## 1. Strategy

Tests follow three non-negotiable principles for this project:

1. **Cost × signal.** The cheapest test that gives a real signal for the
   risk wins. Do not promote to e2e because e2e "feels safer." Do not put a
   vision model on top of a deterministic visual diff that already catches
   the regression.
2. **User concerns are first-class evidence.** Risks anchored in "<the
   team is worried about X, and the failure would surface somewhere in
   <area>>" carry the same weight as PRD lines or hot-spot data.
3. **Risks are scenarios, not code locations.** This plan documents *what
   could fail* and *why we believe it's likely* - drawn from documents,
   interview, and codebase *signal* (churn, structure, test base). It does
   NOT claim to know which line owns the failure. That knowledge is
   produced by `/10x-research` during each rollout phase. If the plan and
   research disagree about where the failure lives, research is the
   ground truth.

Hot-spot scope used for likelihood weighting: `LinuxGameCompat/Data`,
`LinuxGameCompat/Services`, `LinuxGameCompat/Components`,
`LinuxGameCompat/Program.cs`, and `LinuxGameCompat.Tests`, excluding
generated/build output and EF migrations. The 30-day scan had enough signal:
23 scoped commits; hottest directories were `LinuxGameCompat/Components`
(35 file-touches), `LinuxGameCompat/Data` (31), and
`LinuxGameCompat/Services` (21).

## 2. Risk Map

The top failure scenarios this project must protect against, ordered by
risk = impact × likelihood. Risks are failure scenarios in user / business
terms, not test names. The Source column cites the *evidence that surfaced
this risk* - never a specific file as "where the failure lives" (that is
research's job, see §1 principle #3).

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence - not anchor) |
|---|--------------------------|--------|------------|--------------------------------|
| 1 | Passwordless auth signs a user in from an invalid, expired, consumed, or replayed magic link, accepts an unsafe return URL edge case, or exposes more request/account-state signal than intended. | High | High | interview Q1/Q3/Q4; PRD Access Control; archive passwordless-member-access; hot-spot dirs `LinuxGameCompat/Services` and `LinuxGameCompat/Program.cs`; research correction 2026-06-03 |
| 2 | User email, token, or auth material leaks outside its intended boundary: persistence, production email delivery, Development-only manual-smoke logging, failure logs, or retained request metadata. | High | Medium | interview Q4; tech-stack auth enabled; archive passwordless-member-access; research correction 2026-06-03 |
| 3 | Compatibility status, caveat, workaround, or note claims appear without source-backed evidence, creating false certainty. | High | Medium | PRD Guardrails and NFR; archive minimal-evidence-baseline and anonymous-compatibility-lookup |
| 4 | Hidden, suppressed, or missing games become publicly visible or distinguishable through lookup, browse, or detail behavior. | Medium | Medium | PRD public lookup scope; archive minimal-evidence-baseline, anonymous-compatibility-lookup, and browse-available-games; hot-spot dirs `LinuxGameCompat/Data` and `LinuxGameCompat/Services` |
| 5 | Public lookup, browse, detail, login, or navigation behavior regresses without a fast signal before manual testing. | Medium | High | roadmap shipped S-01/S-03/F-02; hot-spot dir `LinuxGameCompat/Components`; sparse test-base profile |

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | Invalid, expired, consumed, and replayed links do not sign in; syntactically valid existing and new email requests have equivalent user-visible outcomes; invalid/send-failure outcomes expose only the intended request-failure class; unsafe slash/backslash return URL edges do not redirect externally. | Happy-path login means auth is safe; account-enumeration risk exists just because auth exists. | Auth entry points, token lifecycle, persisted request state, sign-in boundary, redirect normalization including slash/backslash forms, existing-vs-new valid email equivalence, request-failure behavior. | integration/service tests first; endpoint/browser smoke only where service tests cannot observe cookie or browser redirect behavior. | happy-path-only auth tests; over-mocking identity/cookie behavior; asserting implementation internals instead of sign-in outcome; treating current request-failure UI as account enumeration without evidence. |
| #2 | Raw tokens stay out of persistence and failure logs, production email delivery contains only the intended login link, failed sends do not leave active token rows, and Development full-link logging is either accepted as a local-only manual-smoke tradeoff or deliberately redacted. | Hashing the token is enough to cover privacy; all logging boundaries can be judged by the same rule. | Token generation/storage boundary, email sender/logging boundary, Development-vs-production sender behavior, error paths, retained normalized email/IP/user-agent metadata, failed-send cleanup. | integration tests with fake sender/logger and persistence checks; tiny unit/service test for any redaction decision. | copied production calculation as oracle; testing only success path; ignoring failure logs; writing a blanket "no logs contain raw tokens" test while Development logging intentionally does. |
| #3 | Source-backed claims render or read with verifiable source links, while no-evidence games remain explicitly uncertain. | A compact status label is enough evidence for the user. | Seed/source-of-truth data, claim/source relationship, no-evidence summary behavior, UI/read boundary. | read-service integration tests plus focused route/page checks if research finds UI-only risk. | assertions copied from production mapping; meaningless snapshots; source-link count without claim meaning. |
| #4 | Hidden/suppressed and missing records are excluded or share a generic public outcome across lookup, browse, and detail paths. | One read-service test protects every public route. | Public entry points, hidden filtering boundary, not-found behavior, browse/search/detail data flow. | integration tests around public read contracts, with a thin UI smoke if route behavior differs. | testing only the normal visible record; brittle route-specific duplication. |
| #5 | Critical routes load, navigate, and submit through the user-visible path for lookup, browse, detail, login, and logout states. | Build success means Blazor UI still works. | Render mode, route list, form submission boundaries, auth-aware nav state, app startup dependency shape. | thin browser smoke layer after cheaper backend contracts are covered. | broad e2e suite for every route; pixel snapshots without behavior; UI tests that duplicate backend assertions. |

Future feature gates:

- Member favorites are not a current test target because roadmap S-02 is not
  implemented yet. When S-02 opens, owner-isolation tests are mandatory:
  saved games must bind to the authenticated local member and must not expose
  another member's favorites.
- Scheduled or automated real-data evidence collection is not a current test
  target because it is not implemented or on the active roadmap yet. When it
  opens, the phase must cover provenance, freshness, idempotency, source
  throttling/rate limits, duplicate handling, and protection against bad or
  poisoned external data.

## 3. Phased Rollout

Each row is a discrete rollout phase that will open its own change folder
via `/10x-new`. Status moves left-to-right through the values below; the
orchestrator updates Status as artifacts appear on disk.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|------------|-----------------|---------------|------------|--------|---------------|
| 1 | Auth and privacy regression floor | Defend magic-link auth, redirects, replay, generic responses, and email/token privacy at the cheapest layer. | #1, #2 | integration + service | implementing | testing-auth-privacy-regression-floor |
| 2 | Evidence and public read contracts | Protect source-link integrity, no-evidence uncertainty, hidden records, search, and browse contracts. | #3, #4 | integration + contract | not started | — |
| 3 | Critical UI smoke layer | Add a thin signal for critical lookup, browse, detail, login, logout, and nav behavior. | #5 | browser smoke | not started | — |
| 4 | Quality gates and cookbook | Lock build/test gates and document future favorites plus ingestion test requirements. | cross-cutting | gates + cookbook | not started | — |

Status vocabulary:

| Value | Meaning |
|-------|---------|
| `not started` | No change folder for this rollout phase yet. |
| `change opened` | `context/changes/<id>/` exists with `change.md`; research not done. |
| `researched` | `research.md` exists in the change folder. |
| `planned` | `plan.md` exists with a `## Progress` section. |
| `implementing` | Progress section has at least one `[x]` and at least one `[ ]`. |
| `complete` | Progress section is fully `[x]`. |

## 4. Stack

The classic test base for this project. AI-native tools are not currently
recommended because deterministic tests are cheaper and more directly tied to
the identified risks.

| Layer | Tool | Version | Notes |
|-------|------|---------|-------|
| app/runtime | .NET / ASP.NET Core Blazor | net10.0 | Blazor Server/Razor Components app. |
| persistence | EF Core + Npgsql + PostgreSQL | EF 10.0.8, Npgsql EF 10.0.2 | Existing migrations and PostgreSQL-backed integration tests. |
| auth | ASP.NET Core Identity | 10.0.8 | Passwordless member access via local Identity and magic-link flow. |
| unit + integration | xUnit + Microsoft.NET.Test.Sdk | xUnit 2.9.3, SDK 17.14.1 | Sparse suite: 2 test files clustered around validation and PostgreSQL integration. |
| database test dependency | Testcontainers.PostgreSql | 4.12.0 | Existing tests require Docker/Testcontainers. |
| browser smoke | none yet | n/a | See §3 Phase 3 if research confirms browser smoke is cheapest useful signal. |
| CI gates | none detected | n/a | See §3 Phase 4. No `.github` workflow existed during discovery. |

**Stack grounding tools (current session):**
- Docs: none available in current session - no Context7/framework docs MCP exposed; local manifests/configs were used; checked: 2026-06-02.
- Search: none available in current session - no Exa-style search MCP exposed; checked: 2026-06-02.
- Runtime/browser: none available in current session - no Playwright/browser MCP exposed; possible future smoke layer only if added by Phase 3; checked: 2026-06-02.
- Provider/platform: GitHub connector available - relevant for future PR/issues/actions inspection, but no current CI workflow was present; checked: 2026-06-02.

Use official docs through a docs/search surface if one becomes available
during a rollout phase, especially before adding browser-smoke or CI tooling.
Do not use docs/search to infer code failure anchors; those belong in
per-phase `/10x-research`.

## 5. Quality Gates

The full set of gates that must pass before a change reaches production.
"Required for §3 Phase <N>" means the gate is enforced once that rollout
phase lands; before that, the gate is `planned`.

| Gate | Where | Required? | Catches |
|------|-------|-----------|---------|
| `dotnet build LinuxGameCompat.sln --no-restore` | local | required now after restore | compile/type drift after dependencies are restored. |
| `dotnet test LinuxGameCompat.sln --no-restore` | local | required now after restore | validator and PostgreSQL integration regressions. |
| Auth/privacy regression floor | local + CI once wired | required after §3 Phase 1 | magic-link replay/expiry/redirect/privacy regressions. |
| Evidence/read-contract regression floor | local + CI once wired | required after §3 Phase 2 | source-link, hidden-record, no-evidence, search, and browse contract drift. |
| Critical UI smoke | local + CI once wired | required after §3 Phase 3 if added | broken public routes, nav, login/logout state, and critical form flows. |
| Build/test CI workflow | CI on PR or merge | required after §3 Phase 4 | changes reaching production without automated build/test signal. |

## 6. Cookbook Patterns

How to add new tests in this project. Each sub-section is filled in once
the relevant rollout phase ships; before that, the sub-section reads
"TBD - see §3 Phase <N>."

### 6.1 Adding an auth/privacy regression test

Use service + PostgreSQL integration tests first for passwordless auth and
privacy regressions. Wire real EF Core, ASP.NET Core Identity,
`SignInManager<ApplicationUser>`, `IMagicLinkService`, `TimeProvider`, and
HTTP context state through the shared auth test harness; keep fakes at the
sender/logger boundary where the risk is the outbound artifact or failure log.
Do not mock Identity sign-in internals when the test is meant to prove service
sign-in behavior.

Cover the risk at the behavior boundary:

- valid syntactically correct requests for existing and new emails have the
  same accepted service-visible outcome;
- invalid, expired, consumed, and replayed tokens fail without creating or
  signing in unintended members;
- unsafe absolute, protocol-relative, slash/backslash, encoded slash, encoded
  backslash, blank, and overlong return URLs normalize to `/`, while ordinary
  local paths still round-trip;
- raw tokens are absent from saved `MagicLinkRequest` rows without copying the
  production hash calculation as the test oracle;
- send-failure warning logs omit raw tokens, `token=`, and full login links,
  while normalized email remains allowed operational context;
- failed sends remove the saved magic-link request and leave no active token
  row;
- production email composition contains the intended login link and no extra
  auth material.

Development full-link logging is an explicit local-only manual-smoke exception.
Do not add blanket "no logs contain raw tokens" assertions unless the
Development sender policy changes or is deliberately redacted.

### 6.2 Adding an evidence/read-contract test

TBD - see §3 Phase 2 for source-link integrity, no-evidence uncertainty,
hidden-record exclusion, literal search, and browse contract patterns.

### 6.3 Adding a critical UI smoke test

TBD - see §3 Phase 3 for route, navigation, login/logout, lookup, browse,
and detail smoke patterns.

### 6.4 Adding tests for future member favorites

TBD - future gate. When roadmap S-02 opens, require owner-isolation coverage:
favorites must bind to the authenticated local member, and one member must
not read, mutate, or infer another member's saved games.

### 6.5 Adding tests for future real-data evidence collection

TBD - future gate. When scheduled or automated evidence collection opens,
require coverage for provenance, freshness, import idempotency, duplicate
handling, external source failure/rate-limit behavior, and bad external data.

### 6.6 Per-rollout-phase notes

- 2026-06-03, §3 Phase 1 `testing-auth-privacy-regression-floor`: shipped the
  auth/privacy regression floor for risks #1 and #2 using xUnit,
  Testcontainers PostgreSQL, real EF Core/Identity wiring, fake sender/logger
  boundaries, and small internal helper seams.
- Cheapest useful layer: service + PostgreSQL integration tests covered token
  lifecycle, request equivalence, return URL hardening, persistence privacy,
  failure-log privacy, failed-send cleanup, production email composition, and
  the Development full-link logging exception. Endpoint/browser cookie and
  `Location` proof remains deferred to the critical UI smoke rollout.
- AI-native guidance checked 2026-06-03: no AI-native testing layer is
  recommended for this phase because deterministic xUnit/Testcontainers
  coverage is cheaper and directly tied to risks #1 and #2.

## 7. What We Deliberately Don't Test

Exclusions agreed during the rollout interview and seed brief. Future
contributors should respect these unless the underlying assumption changes.

- **Not-yet-implemented member favorites** - do not create current tests for
  behavior that does not exist yet. Re-evaluate when roadmap S-02 opens.
- **Not-yet-implemented real-data evidence collection** - do not create
  current tests for scheduled/import behavior until the feature is specified
  and implemented.
- **Broad route-by-route e2e coverage** - avoid a large browser suite until
  cheaper integration/contract tests no longer catch the relevant failure.
- **AI-native visual or review layers** - no current risk needs them; add
  only after deterministic tests prove insufficient.

## 8. Freshness Ledger

- Strategy (§1-§5) last reviewed: 2026-06-02
- Stack versions last verified: 2026-06-02
- AI-native tool references last verified: 2026-06-02

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive,
- a recommended tool's `checked:` date is older than three months,
- the project's tech stack changes (new framework, new test runner),
- §7 negative-space no longer matches what the team believes.
