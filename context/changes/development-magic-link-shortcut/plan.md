# Development Magic Link Shortcut Implementation Plan

## Overview

Add an explicitly configurable shortcut that shows generated passwordless magic links in the `/login` success UI so local and hosted test deployments can exercise authenticated features without SMTP. The shortcut is controlled by `Auth:ShowMagicLinksInFrontend`, may be enabled in any environment by deliberate configuration, and must render a clear test-only warning whenever it exposes a login link.

## Current State Analysis

Passwordless auth already exists through `IMagicLinkService`, the `/auth/magic-link/request` endpoint, and the `/login` page. Development currently uses `LoggingAuthEmailSender` to log full magic links, while non-Development uses SMTP. The generated link is built inside `MagicLinkService.RequestLoginLinkAsync`, sent through `IAuthEmailSender`, and then discarded because `MagicLinkRequestResult` only reports whether the request was accepted.

The app has no existing TempData or MVC ViewFeatures usage, so the shortcut needs a small purpose-built protected-cookie helper for cookie-backed one-time display state. The test suite already has PostgreSQL-backed auth/privacy tests and an auth harness, but no browser or HTTP-host test infrastructure.

## Desired End State

When `Auth:ShowMagicLinksInFrontend` is false or absent, login request behavior is unchanged: a successful request redirects to `/login?sent=1`, tells the user to check their inbox, and exposes no link in the frontend.

When `Auth:ShowMagicLinksInFrontend` is true, a login request with valid input can complete without SMTP: the app persists the one-use magic-link request, redirects to `/login?sent=1`, and shows the generated link inside the existing success panel with explicit warning copy. Clicking that link signs in through the normal consume endpoint, preserves existing token lifecycle rules, and redirects to the stored local return URL.

### Key Discoveries:

- `Program.cs` maps the request endpoint and currently redirects based only on `MagicLinkRequestResult.Accepted`.
- `IMagicLinkService` currently exposes `MagicLinkRequestResult(bool Accepted)`, so the generated URI needs a deliberate result-contract change.
- `MagicLinkService.RequestLoginLinkAsync` already builds the exact login link before sending it through `IAuthEmailSender`.
- `Program.cs` registers `LoggingAuthEmailSender` only when `builder.Environment.IsDevelopment()` is true, and that sender intentionally logs full links today.
- `SmtpAuthEmailSender` throws when SMTP is not configured, so the enabled frontend shortcut path must not require email delivery to succeed.
- `Login.razor` already has a success panel for `sent=1`, making it the natural UI surface for the shortcut, but it is an `InteractiveServer` Blazor component rather than a Razor Page with `[TempData]`.
- `AuthPrivacyRegressionTests` already cover return URL hardening, token persistence, replay/failure behavior, and the Development full-link logging exception.

## What We're NOT Doing

- No new authentication mechanism.
- No password login, OAuth, passkeys, roles, or account-management UI.
- No database schema or migration.
- No magic-link archive, admin diagnostics page, or list of recent generated links.
- No browser automation or new UI test framework.
- No startup block when the flag is enabled in Production; this is explicitly allowed by configuration.
- No change to existing Development full-link logging behavior.
- No SMTP provider selection or hosting-email integration.

## Implementation Approach

Keep the existing magic-link flow authoritative and add only an opt-in display surface around the generated link. The service contract should expose the generated link only when the caller requests it for the frontend shortcut path, avoiding accidental raw-token propagation in default service use. In the enabled shortcut path, successful token persistence is enough to show the link even when SMTP delivery is unavailable; the disabled/default path must keep the existing send-failure cleanup and failure redirect behavior. The endpoint should bridge the POST-to-GET redirect with a purpose-built protected-cookie one-time handoff rather than query strings, so the raw token is not placed in browser history or proxy logs by the redirect itself.

## Critical Implementation Details

### User Experience Spec

The visible link must appear only inside the `sent=1` login success state and only when the one-time display value exists. The warning copy should make clear that the displayed URL is a configured test shortcut and that anyone with the link can sign in.

## Phase 1: Shortcut Contract And Configuration

### Overview

Add the configuration switch, expose the generated link through an explicit request path, and hand the link from the POST endpoint to the redirected login page without changing default auth behavior.

### Changes Required:

#### 1. Magic-Link Service Contract

**File**: `LinuxGameCompat/Services/IMagicLinkService.cs`

**Intent**: Let the request flow optionally return the generated login URI for the frontend shortcut while keeping the default service result free of raw auth tokens.

**Contract**: Extend the request input or service call contract with an explicit "include generated link" signal, and extend `MagicLinkRequestResult` with an optional `Uri? LoginLink` or equivalent nullable generated-link property. Default callers must receive `Accepted=true` with no link unless they opted into link return.

#### 2. Magic-Link Service Implementation

**File**: `LinuxGameCompat/Services/MagicLinkService.cs`

**Intent**: Preserve existing token generation, persistence, email sending, send-failure cleanup, and consume behavior while returning the generated URI only for the configured display path.

**Contract**: `RequestLoginLinkAsync` continues to build one link and send that same link through `IAuthEmailSender` when email delivery is part of the selected path. Validation failures always return `Accepted=false` with no link. Send failure behavior depends on the explicit frontend-display opt-in: default callers keep the current cleanup behavior and receive `Accepted=false`, while opted-in callers keep the persisted request and receive `Accepted=true` with the generated link so trusted test deployments can log in without SMTP. The send-failure exception must not log the raw token or full link.

#### 3. Endpoint Configuration And One-Time Handoff

**File**: `LinuxGameCompat/Program.cs`

**Intent**: Make frontend link display controlled by `Auth:ShowMagicLinksInFrontend` and carry the generated link through the existing redirect flow with a small helper tailored to minimal APIs and Blazor.

**Contract**: Add a small purpose-specific protected-cookie helper for this single display value, for example under `LinuxGameCompat/Services/Auth/` if the auth service changes reach three related files. The helper must use ASP.NET Core data protection, write one configured cookie name/path for the generated link, read and delete it during the redirected `/login?sent=1` initial request, and expose clear `Set`, `TryConsume`, and `Clear` operations over `HttpContext`. Do not add MVC TempData/ViewFeatures for this shortcut. In `/auth/magic-link/request`, read `Auth:ShowMagicLinksInFrontend` as a boolean, pass the opt-in signal to the service, clear any existing display-link handoff before processing, and when the result is accepted with a generated link, store that link before redirecting to `/login?sent=1`. Disabled or failed requests must clear the display-link key and must not leave stale values available to the next login page load.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Service tests prove accepted requests do not include a generated link by default.
- Service tests prove opted-in accepted requests include the same generated link sent through `IAuthEmailSender`.
- Existing auth/privacy tests for token hashing, send failure cleanup, replay, expiry, and return URL normalization still pass.

#### Manual Verification:

- With `Auth:ShowMagicLinksInFrontend` absent or false, submitting `/login` still shows only the existing inbox success message.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Login UI Display And Styling

### Overview

Render the generated link in the existing login success panel when the shortcut is enabled and the redirected page has a one-time display value.

### Changes Required:

#### 1. Login Page Display

**File**: `LinuxGameCompat/Components/Pages/Login.razor`

**Intent**: Show testers the generated magic link immediately after a successful request without changing the normal disabled-state login UX.

**Contract**: Read the one-time display value during the redirected `sent=1` page load using the Phase 1 handoff API in a Blazor-compatible way: the value must be available on the initial render and consumed so refresh/back navigation does not redisplay a stale link. If a link is present, render it inside the current success panel as a clickable link with warning text. If no link is present, keep the current "Check your inbox" copy.

#### 2. Login Shortcut Styling

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Make the exposed URL readable and safe on narrow screens without disturbing the existing login form layout.

**Contract**: Add focused styles for the shortcut warning/link block under the login success state. Long URLs must wrap, remain clickable, and not overflow their parent on mobile.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual Verification:

- With `Auth:ShowMagicLinksInFrontend=true`, submitting `/login` shows the generated link inline in the success panel.
- The success panel includes clear test-only warning copy.
- The generated link wraps cleanly on a narrow/mobile viewport.
- Clicking the shown link signs in through `/auth/magic-link/consume` and redirects to the stored local return URL.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Verification And Handoff

### Overview

Add focused regression coverage for the shortcut boundaries and document the hosted-test configuration path.

### Changes Required:

#### 1. Auth Regression Tests

**File**: `LinuxGameCompat.Tests/AuthPrivacyRegressionTests.cs`

**Intent**: Cover the raw-link exposure boundary without adding browser tooling.

**Contract**: Add focused tests for the new service result contract and any small endpoint/handoff seam introduced by Phase 1. Assertions must prove disabled/default behavior exposes no generated link, enabled behavior exposes the generated link only after an accepted request, and failed requests do not expose stale or new links.

#### 2. PostgreSQL Auth Compatibility Tests

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Keep the existing PostgreSQL-backed auth regression suite aligned with the request/result contract change.

**Contract**: Review the existing `RequestLoginLinkAsync` call sites and `Accepted` assertions, update only the calls affected by the new opt-in/default contract, and preserve the existing token hashing, send failure cleanup, replay, expiry, and local return URL coverage.

#### 3. Auth Test Harness Support

**File**: `LinuxGameCompat.Tests/AuthTestHarness.cs`

**Intent**: Support the new auth tests with minimal helper changes that match existing test style.

**Contract**: Add only the helper surface needed to request generated-link behavior or inspect the display handoff seam. Do not weaken existing fake sender, time provider, or current-member behavior.

#### 4. Configuration Documentation

**File**: `README.md`

**Intent**: Make the hosted-test shortcut discoverable and explicitly risky so future operators do not mistake it for normal production email behavior.

**Contract**: Add a short auth/development configuration note for `Auth:ShowMagicLinksInFrontend`, including that it exposes bearer login links in the frontend and should only be enabled for trusted local/test/demo deployments.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`
- New tests prove disabled/default behavior does not surface generated links.
- New tests prove failed requests do not leave a frontend-display link.

#### Manual Verification:

- Hosted or local run with `Auth:ShowMagicLinksInFrontend=true` can complete login without SMTP.
- Hosted or local run with the flag disabled preserves the normal email-only UX.
- Existing Development logs still include full magic links as before.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before marking the change implemented.

---

## Testing Strategy

### Unit Tests:

- Service result defaults: accepted request without opt-in has no generated link.
- Service opt-in: accepted request returns the same URI passed to the configured sender.
- Failed requests: invalid email returns no generated link; send failure keeps cleanup/no-link behavior by default and returns the generated link only for the explicit frontend shortcut opt-in.
- Display handoff seam: enabled configuration stores the link only after accepted requests; disabled configuration and failed/default requests clear stale display values.

### Integration Tests:

- Existing PostgreSQL-backed auth/privacy tests remain authoritative for token hashing, deferred member creation, expiry, replay, default send-failure cleanup, and local return URL normalization.
- No new database schema tests are needed because the shortcut does not persist new data.

### Manual Testing Steps:

1. Run the app with `Auth:ShowMagicLinksInFrontend` absent or false.
2. Submit `/login` with a valid email and confirm the success panel tells the user to check their inbox with no visible link.
3. Run the app with `Auth:ShowMagicLinksInFrontend=true`.
4. Submit `/login` with a valid email and confirm the success panel shows the warning and generated link.
5. Click the generated link and confirm the user is signed in.
6. Repeat with a login return URL such as `/games/baldurs-gate-3` and confirm the consume flow redirects locally.
7. Reuse the same link and confirm the existing expired/used-link failure UI appears.

## Performance Considerations

The shortcut adds negligible runtime cost. The one-time handoff should store only one generated link for one redirected response, not a collection or history of links.

## Migration Notes

No database migration is required. Hosted test deployments that need the shortcut must set `Auth:ShowMagicLinksInFrontend=true` through normal app configuration. Existing SMTP configuration remains required for production email delivery when the shortcut is disabled; when the shortcut is enabled for trusted test/demo deployments, SMTP may be absent and the generated link is shown in the frontend after token persistence.

## References

- Roadmap slice: `context/foundation/roadmap.md` S-07.
- Prior auth plan: `context/archive/2026-05-31-passwordless-member-access/plan.md`.
- Auth/privacy research: `context/archive/2026-06-03-testing-auth-privacy-regression-floor/research.md`.
- Request endpoint: `LinuxGameCompat/Program.cs`.
- Magic-link service: `LinuxGameCompat/Services/MagicLinkService.cs`.
- Login page: `LinuxGameCompat/Components/Pages/Login.razor`.
- Auth regression tests: `LinuxGameCompat.Tests/AuthPrivacyRegressionTests.cs`.
- PostgreSQL compatibility tests: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Shortcut Contract And Configuration

#### Automated

- [x] 1.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore` — fa98076
- [x] 1.2 Service tests prove accepted requests do not include a generated link by default. — fa98076
- [x] 1.3 Service tests prove opted-in accepted requests include the same generated link sent through `IAuthEmailSender`. — fa98076
- [x] 1.4 Existing auth/privacy tests for token hashing, send failure cleanup, replay, expiry, and return URL normalization still pass. — fa98076

#### Manual

- [x] 1.5 With `Auth:ShowMagicLinksInFrontend` absent or false, submitting `/login` still shows only the existing inbox success message. — fa98076

### Phase 2: Login UI Display And Styling

#### Automated

- [x] 2.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [x] 2.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`

#### Manual

- [x] 2.3 With `Auth:ShowMagicLinksInFrontend=true`, submitting `/login` shows the generated link inline in the success panel.
- [x] 2.4 The success panel includes clear test-only warning copy.
- [x] 2.5 The generated link wraps cleanly on a narrow/mobile viewport.
- [x] 2.6 Clicking the shown link signs in through `/auth/magic-link/consume` and redirects to the stored local return URL.

### Phase 3: Verification And Handoff

#### Automated

- [ ] 3.1 Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- [ ] 3.2 Tests pass: `dotnet test LinuxGameCompat.sln --no-restore`
- [ ] 3.3 New tests prove disabled/default behavior does not surface generated links.
- [ ] 3.4 New tests prove failed requests do not leave a frontend-display link.

#### Manual

- [ ] 3.5 Hosted or local run with `Auth:ShowMagicLinksInFrontend=true` can complete login without SMTP.
- [ ] 3.6 Hosted or local run with the flag disabled preserves the normal email-only UX.
- [ ] 3.7 Existing Development logs still include full magic links as before.
