<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Passwordless Member Access Implementation Plan

- **Plan**: `context/changes/passwordless-member-access/plan.md`
- **Mode**: Deep
- **Date**: 2026-05-31
- **Verdict**: REVISE
- **Findings**: 1 critical 2 warnings 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | WARNING |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | FAIL |
| Plan Completeness | WARNING |

## Grounding

Grounding: 10/10 existing paths ✓, 10/10 symbols ✓, brief↔plan ✓. Current build: `dotnet build LinuxGameCompat.sln --no-restore` passes.

## Findings

### F1 — GET magic-link consumption is vulnerable to email scanners

- **Severity**: ❌ CRITICAL
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 2 — Auth Endpoints
- **Detail**: The plan makes `GET /auth/magic-link/consume` perform one-time token consumption, member creation, and sign-in. In production, email clients and security scanners may prefetch links. That can consume the token before the user clicks it, breaking the core login flow.
- **Fix ⭐ Recommended**: Change GET consume into a confirmation page, then consume/sign in only on POST with antiforgery protection.
  - Strength: Preserves one-use semantics while avoiding scanner-triggered login.
  - Tradeoff: Adds one confirmation click/page to the login flow.
  - Confidence: HIGH — the plan already has POST endpoints and antiforgery middleware.
  - Blind spot: Exact UI copy and failure page shape still need specifying.
- **Decision**: PENDING

### F2 — Request throttling is promised but not planned

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 1 / Performance Considerations
- **Detail**: The plan says request metadata is suitable for throttling and that basic per-email throttling is enough for MVP, but no phase implements or tests throttling. Without it, `POST /auth/magic-link/request` can send unlimited emails or produce unlimited logged links.
- **Fix**: Add explicit per-normalized-email throttling to `MagicLinkService` request handling, with generic responses and a test for throttled requests.
  - Strength: Closes the abuse gap with minimal scope.
  - Tradeoff: Requires choosing a simple limit/window in the plan.
  - Confidence: HIGH — the planned `MagicLinkRequest` table already stores the needed data.
  - Blind spot: IP-based throttling is still intentionally out of scope unless required.
- **Decision**: PENDING

### F3 — Auth endpoint tests need a web-host strategy

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Completeness
- **Location**: Phase 4 — Integration Tests
- **Detail**: Phase 4 puts auth lifecycle tests into `PostgreSqlCompatibilityTests.cs`, but the current fixture only creates `CompatibilityDbContext` and service instances. It cannot verify endpoint routing, redirects, antiforgery, cookies, or real sign-in behavior. The test project also has no `WebApplicationFactory` / ASP.NET Core test-host package today.
- **Fix**: Add a web-host integration test strategy: include the needed test-host package, boot the app against Testcontainers PostgreSQL, replace email sending with a capture fake, and verify request/confirm/logout over HTTP.
  - Strength: Tests the actual auth surface instead of only persistence.
  - Tradeoff: Adds test infrastructure beyond the existing DB-only fixture.
  - Confidence: HIGH — current tests show only DbContext/Testcontainers wiring.
  - Blind spot: Some pure token/return-url tests can still stay as service tests.
- **Decision**: PENDING
