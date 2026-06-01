<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Passwordless Member Access

- **Plan**: `context/changes/passwordless-member-access/plan.md`
- **Scope**: Phases 1-4 of 4
- **Date**: 2026-06-01
- **Verdict**: REJECTED
- **Findings**: 1 critical, 2 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | FAIL |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 - Production magic links can use attacker-controlled Host

- **Severity**: CRITICAL
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Program.cs:116`
- **Detail**: `GetPublicBaseUri` falls back to `request.Scheme` and `request.Host` when `Auth:PublicBaseUrl` is absent. In production, a spoofed Host header can cause the app to email a valid login token inside an attacker-controlled URL, leaking the token if the user clicks it.
- **Fix**: In non-development, require an absolute HTTPS `Auth:PublicBaseUrl` and fail clearly if it is missing or non-HTTPS. Keep request-derived fallback only for Development.
  - Strength: Removes the host-header token leak and matches the documented production requirement.
  - Tradeoff: Production startup/request config becomes stricter.
  - Confidence: HIGH - single helper controls all magic-link origins.
  - Blind spot: Whether deployed reverse-proxy host filtering exists was not verified, but the app should not depend on it here.
- **Decision**: PENDING

### F2 - Logout form token is rendered but not enforced

- **Severity**: WARNING
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `LinuxGameCompat/Program.cs:98`
- **Detail**: The plan requires `POST /logout` to have antiforgery protection. The nav form emits `<AntiforgeryToken />`, but the endpoint only has `.RequireAuthorization()` and no endpoint metadata that requires antiforgery validation.
- **Fix**: Add explicit antiforgery validation metadata/handling to `/logout` while keeping the existing form token.
- **Decision**: PENDING

### F3 - Login request failures can leave active tokens or return 500s

- **Severity**: WARNING
- **Impact**: MEDIUM - real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/MagicLinkService.cs:43`
- **Detail**: The service persists a magic-link request before email construction/send. Malformed or overlong direct POST inputs can hit DB/mail exceptions, and SMTP outages can bubble as 500s after an active token is stored.
- **Fix**: Validate and bound email/return-url input before persistence, and handle email-send failures deliberately by logging and invalidating/removing the saved request or returning a controlled generic failure.
  - Strength: Keeps generic UX while preventing stale active tokens and noisy external-boundary failures.
  - Tradeoff: Requires choosing whether failed sends should still look like accepted requests to the browser.
  - Confidence: MED - direct endpoint abuse and SMTP failure paths were inferred from code, not exercised in tests.
  - Blind spot: Current production SMTP behavior under provider failures was not tested.
- **Decision**: PENDING

### F4 - Magic-link request throttling remains a launch risk

- **Severity**: OBSERVATION
- **Impact**: MEDIUM - real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Program.cs:89`
- **Detail**: The unauthenticated request endpoint can generate/store/send links without throttling. The plan explicitly defers throttling, so this is not drift, but it remains a public-launch abuse and table-growth risk.
- **Fix**: Before public or higher-volume exposure, add per-normalized-email and preferably per-IP throttling using `CreatedAt`, with generic responses.
- **Decision**: PENDING

## Verification

- **PASS**: `dotnet build LinuxGameCompat.sln --no-restore` - build succeeded with 0 warnings and 0 errors.
- **PASS**: `dotnet test LinuxGameCompat.sln --no-restore` - 33 passed, 0 failed, 0 skipped.
- **Manual**: The plan's manual checkboxes are marked complete. A live browser smoke test was not re-run during this review.

## Scope Notes

No favorites, password UI, recovery, profile management, admin/roles, OAuth/passkeys, E2E framework, or provider-specific email API were added.
