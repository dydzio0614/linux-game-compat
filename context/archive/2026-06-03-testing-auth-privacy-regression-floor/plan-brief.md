# Auth And Privacy Regression Floor - Plan Brief

> Full plan: `context/changes/testing-auth-privacy-regression-floor/plan.md`
> Research: `context/changes/testing-auth-privacy-regression-floor/research.md`

## What & Why

This plan rolls out Phase 1 of `context/foundation/test-plan.md`: a regression
floor for passwordless magic-link auth safety and token/email privacy. It covers
risks #1 and #2 without expanding into browser smoke or endpoint-host testing
before the cheaper service/integration layer is exhausted.

## Starting Point

The app already has Identity-backed magic-link auth and 39 passing tests.
Existing PostgreSQL integration tests cover several auth basics, but research
found gaps around existing-vs-new email equivalence, slash/backslash return URL
edges, failure-log privacy, production email content, and the Development
full-link logging exception.

## Desired End State

Auth/privacy tests live in a focused test area, use real EF Core and Identity
wiring, and prove the important service-level contracts. Unsafe return URLs are
hardened and covered, raw tokens stay out of persistence and failure logs, and
the foundation test-plan cookbook records the shipped pattern for future phases.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Test layer | Service + PostgreSQL integration only | Cheapest layer observes the token lifecycle, persisted request state, and sender/logging boundaries. | Research / Plan |
| Test structure | Dedicated auth/privacy test file | Keeps risk #1/#2 readable instead of growing the broad compatibility test class. | Plan |
| Development logging | Accept full-link logging as local-only exception | Current Development sender intentionally supports manual smoke by logging the full link. | Research / Plan |
| Failed-send logs | Keep normalized email, forbid raw token/link | Email is useful operational context, while raw auth material is not. | Research / Plan |
| Return URL edge | Fix then test slash/backslash forms | Research found this as the highest-signal redirect ambiguity not covered today. | Research / Plan |
| AI-native tools | Not recommended as of 2026-06-03 | Deterministic xUnit/Testcontainers tests are cheaper and directly tied to risks #1/#2. | Test Plan / Plan |

## Scope

**In scope:**

- Extract shared PostgreSQL/auth test support.
- Add dedicated auth/privacy regression tests.
- Harden shared local return URL normalization.
- Add a tiny internal SMTP message composition test seam if needed.
- Update `context/foundation/test-plan.md` §6.1 and §6.6 after patterns ship.

**Out of scope:**

- TestServer, browser smoke, Playwright, bUnit, and broad e2e coverage.
- Actual auth-cookie issuance proof or browser `Location` behavior.
- Request throttling, favorites, account recovery, and real SMTP provider tests.
- Blanket "no logs contain raw tokens" assertions while Development logging intentionally emits full links.

## Architecture / Approach

The rollout keeps auth verification near the service boundary: xUnit tests use
PostgreSQL Testcontainers, real EF Core, real Identity service wiring, fake
sender/logger boundaries, and small internal helper seams exposed to tests by
the existing `InternalsVisibleTo("LinuxGameCompat.Tests")`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Extract Auth Test Support | Reusable fixture/harness and stable existing tests | Accidental behavior change during structural extraction |
| 2. Auth Safety Regressions And Return URL Hardening | Existing-vs-new equivalence, token failure coverage, unsafe URL hardening | Open redirect edge or auth lifecycle drift |
| 3. Privacy Boundary Regressions | Persistence, failure-log, sender, failed-send, and Development exception coverage | Raw token leakage or overbroad privacy assertions |
| 4. Cookbook And Handoff Gates | §6 cookbook patterns and final test gate | Future phases repeat avoided anti-patterns |

**Prerequisites:** Docker/Testcontainers available; dependencies already restored for `--no-restore` gates.
**Estimated effort:** ~2-3 implementation sessions across 4 focused phases.

## Open Risks & Assumptions

- Endpoint/browser auth-cookie proof remains deferred to the critical UI smoke rollout unless a service test proves insufficient.
- Development full-link logging remains acceptable only as a local manual-smoke tradeoff.
- Normalized email remains allowed in send-failure warnings for diagnostics.

## Success Criteria (Summary)

- `dotnet build LinuxGameCompat.sln --no-restore` and `dotnet test LinuxGameCompat.sln --no-restore` pass.
- New auth/privacy tests cover risks #1 and #2 at the service/integration layer without brittle implementation mirrors.
- `context/foundation/test-plan.md` §6 documents the shipped cookbook pattern and dated AI-native guidance.
