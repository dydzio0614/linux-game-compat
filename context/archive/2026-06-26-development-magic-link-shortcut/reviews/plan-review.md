<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Development Magic Link Shortcut

- **Plan**: `context/changes/development-magic-link-shortcut/plan.md`
- **Mode**: Deep
- **Date**: 2026-06-26
- **Verdict**: SOUND
- **Findings**: 0 critical 0 warnings 0 observations after triage

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | PASS |
| Plan Completeness | PASS |

## Grounding

Grounding: 8/8 paths ✓, 8/8 symbols ✓, brief↔plan ✓

## Findings

### F1 — Shortcut still depends on successful email delivery

- **Severity**: ❌ CRITICAL
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: End-State Alignment
- **Location**: Desired End State; Phase 1 — Magic-Link Service Implementation
- **Detail**: The stated goal is hosted/local login without SMTP, but the original Phase 1 contract kept `RequestLoginLinkAsync` returning `Accepted=false` on send failure. In non-Development, `SmtpAuthEmailSender` throws when SMTP is not configured, so a hosted test deployment with no SMTP would redirect to `/login?requestFailed=1` and never show the generated link.
- **Fix**: Define enabled shortcut mode as allowed to accept and return the link after token persistence even if SMTP delivery fails, while preserving existing cleanup/failure behavior when the flag is disabled.
- **Decision**: FIXED — plan now states the enabled shortcut path can complete without SMTP after token persistence, while the disabled/default path keeps existing send-failure cleanup and failure redirect behavior.

### F2 — TempData access is underspecified for Blazor InteractiveServer

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Architectural Fitness
- **Location**: Phase 1 / Phase 2 — One-Time Handoff and Login Page Display
- **Detail**: `Login.razor` is an `@rendermode InteractiveServer` component, not a Razor Page with `[TempData]`. The original plan said to read TempData during the redirected page load, but did not specify a Blazor-compatible access path, service registrations, or cookie deletion timing.
- **Fix**: Use a small one-time protected-cookie helper tailored to the minimal API + Blazor component architecture.
- **Decision**: FIXED — plan now requires a purpose-built protected-cookie helper using ASP.NET Core data protection, with `Set`, `TryConsume`, and `Clear` operations over `HttpContext`, and explicitly says not to add MVC TempData/ViewFeatures for this shortcut.

### F3 — Failed/disabled requests do not explicitly clear stale display links

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 1 — Endpoint Configuration And One-Time Handoff
- **Detail**: The original plan said disabled or failed requests must not write a display link, but did not explicitly require clearing any existing handoff value. With cookie-backed handoff state, “do not write” is not the same as “remove stale value.”
- **Fix**: Add a Phase 1 contract line requiring the endpoint/handoff service to clear the display-link key before processing or on every non-display result.
- **Decision**: FIXED — plan now requires clearing any existing display-link handoff before processing and clearing the key for disabled or failed requests.

### F4 — PostgreSqlCompatibilityTests caller blast radius is omitted

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 3 — Auth Regression Tests
- **Detail**: The original plan named `AuthPrivacyRegressionTests.cs`, but `PostgreSqlCompatibilityTests.cs` also has multiple `RequestLoginLinkAsync` call sites and direct `Accepted` assertions. Contract changes may compile cleanly if defaults are used, but the plan should still call out that this file is part of the blast radius.
- **Fix**: Add `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs` to Phase 3 as a verify/update target for request input/result contract changes.
- **Decision**: FIXED — plan now includes `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs` as a Phase 3 verification target and reference.

## Triage Summary

Fixed: F1, F2, F3, F4 (4)
Skipped: none
Accepted: none
Dismissed: none

► Verdict after fixes: REVISE → SOUND
