# Passwordless Member Access Implementation Plan

## Overview

Add passwordless member identity so later favorites work can attach saved games to a logged-in member. The slice uses email magic links, ASP.NET Core Identity, the existing PostgreSQL database, a 30-day persistent auth cookie, and minimal login/logout UI.

## Current State Analysis

The app has anonymous lookup and PostgreSQL-backed compatibility data, but no authentication stack. `Program.cs` registers Razor Components, EF Core, and `IGameCompatibilityReadService`; it does not register Identity, cookie auth, authorization, auth state, or auth middleware. The visible app surface is lookup-focused: the nav contains only `Lookup`, the root page performs anonymous search, and `/games/{Slug}` shows public evidence detail.

The existing data path already uses EF Core migrations and PostgreSQL integration tests through Testcontainers. Passwordless member access should therefore extend the existing persistence and verification approach instead of introducing a separate auth store.

## Desired End State

Visitors can request a passwordless login link by email, consume a valid one-time magic link, become signed in as a member, and sign out. Unknown emails create a member only after successful link consumption. The app exposes a small service-level contract that future favorites can use to identify the current member, while favorites themselves remain out of scope.

### Key Discoveries:

- `Program.cs` currently has no auth registration or middleware, so this change must add the auth pipeline deliberately.
- `CompatibilityDbContext` is the existing EF Core/PostgreSQL boundary and should own the Identity and magic-link tables.
- `Home.razor` and `GameDetail.razor` already serve anonymous lookup, so auth must preserve public access to both routes.
- `NavMenu.razor` has only the lookup route today; login/logout UI is a new visible surface.
- The PRD locks the product scope: passwordless login is must-have, no account recovery for MVP, and no admin role.

## What We're NOT Doing

- No favorites schema or favorites UI.
- No account recovery, password login, profile management, email change, or account deletion.
- No admin role, invite workflow, or role management.
- No passkeys, external OAuth providers, or hosted identity service integration.
- No browser E2E test framework.
- No production email-provider-specific API integration.

## Implementation Approach

Use ASP.NET Core Identity for maintained user, token, and cookie primitives, backed by the existing PostgreSQL database. Add a small app-specific magic-link request table to track hashed one-time tokens, expiry, consumption, return URL, and request metadata. Add minimal Razor/Blazor UI for requesting login and signing out, then keep the authenticated member contract behind a small service for future favorites.

## Critical Implementation Details

### Timing & lifecycle

Magic-link consumption must mark a token consumed before or atomically with sign-in so a double-click or replay cannot create two valid sessions from the same link. Return URLs must be local-only before redirecting after sign-in.

### User experience spec

Login request responses must be generic. The UI should not reveal whether an email already has an account, because auto-create happens only after a valid link is consumed.

## Phase 1: Identity Persistence And Auth Infrastructure

### Overview

Add Identity and app-specific member-auth persistence, then wire the ASP.NET Core auth pipeline without changing the anonymous lookup routes.

### Changes Required:

#### 1. Identity Packages And User Model

**File**: `LinuxGameCompat/LinuxGameCompat.csproj`

**Intent**: Add the framework packages needed for ASP.NET Core Identity with EF Core storage.

**Contract**: Reference the ASP.NET Core Identity EF Core package version compatible with the existing `net10.0` app and EF Core package versions.

**File**: `LinuxGameCompat/Data/ApplicationUser.cs`

**Intent**: Define the local member identity type used by Identity and future member-owned data.

**Contract**: `ApplicationUser` inherits from Identity's user type and uses normalized email as the login identity. Future favorites should attach to the local user id, not directly to an email string.

#### 2. DbContext And Auth Schema

**File**: `LinuxGameCompat/Data/CompatibilityDbContext.cs`

**Intent**: Extend the existing compatibility database context to include Identity and magic-link persistence.

**Contract**: The context inherits from the appropriate Identity EF Core context for `ApplicationUser`, keeps existing compatibility `DbSet` members and mappings, and adds a `DbSet<MagicLinkRequest>`.

**File**: `LinuxGameCompat/Data/MagicLinkRequest.cs`

**Intent**: Store one-time magic-link request state separately from user records.

**Contract**: The model includes normalized email, token hash, expiry timestamp, consumed timestamp, optional return URL, created timestamp, and request metadata suitable for basic audit or throttling.

**File**: `LinuxGameCompat/Migrations/*`

**Intent**: Add an explicit EF Core migration for Identity tables and magic-link requests.

**Contract**: Existing compatibility tables and seed data remain intact; migration applies cleanly after the current migrations.

#### 3. Auth Pipeline Configuration

**File**: `LinuxGameCompat/Program.cs`

**Intent**: Register Identity, cookie auth, authorization, auth-state services, and auth middleware.

**Contract**: Configure Identity for passwordless member access: unique email required, passwords not exposed in UI, 30-day persistent app cookie, and no roles/admin policy. Add `UseAuthentication()` before `UseAuthorization()` and before Razor Components are mapped.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Migration applies cleanly in PostgreSQL-backed tests
- Existing compatibility read-service tests still pass

#### Manual Verification:

- Anonymous `/` lookup remains reachable without signing in
- Anonymous `/games/{slug}` detail remains reachable without signing in

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Magic-Link Request And Consumption Flow

### Overview

Implement the passwordless login flow: request a link, send it, consume it once, create the member on verified first sign-in, and issue the auth cookie.

### Changes Required:

#### 1. Magic-Link Service

**File**: `LinuxGameCompat/Services/IMagicLinkService.cs`

**Intent**: Define the application boundary for requesting and consuming passwordless login links.

**Contract**: Expose request and consume operations that accept email, return URL, and cancellation token inputs and return result objects without exposing raw token hashes.

**File**: `LinuxGameCompat/Services/MagicLinkService.cs`

**Intent**: Implement secure token creation, storage, validation, one-time consumption, member creation, and sign-in coordination.

**Contract**: Tokens are generated from cryptographically secure random bytes, only token hashes are stored, validity is 15 minutes, links are one-use, unknown emails create an `ApplicationUser` only after valid token consumption, and invalid/expired/consumed tokens produce safe generic failures.

#### 2. Email Sender Abstraction

**File**: `LinuxGameCompat/Services/IAuthEmailSender.cs`

**Intent**: Keep email delivery provider-neutral.

**Contract**: Define a method to send a login link to an email address with no dependency on a specific vendor API.

**File**: `LinuxGameCompat/Services/SmtpAuthEmailSender.cs`

**Intent**: Send production magic-link emails through SMTP configuration.

**Contract**: Read SMTP host, port, credentials, sender address, and TLS settings from configuration. Production should fail clearly if required settings are missing.

**File**: `LinuxGameCompat/Services/LoggingAuthEmailSender.cs`

**Intent**: Support local development without external email credentials.

**Contract**: In development, log the generated login link through app logging so manual smoke tests can consume it.

#### 3. Auth Endpoints

**File**: `LinuxGameCompat/Program.cs`

**Intent**: Add minimal auth endpoints for request, consume, and logout operations.

**Contract**: Add `POST /auth/magic-link/request`, `GET /auth/magic-link/consume`, and `POST /logout`. Request responses are generic. Consume validates local return URLs before redirecting. Logout requires antiforgery protection and redirects to `/`.

### Success Criteria:

#### Automated Verification:

- Magic-link request stores a hashed token and does not create a member immediately
- Valid magic-link consumption creates a member for a new email and marks the request consumed
- Reusing a consumed token fails
- Expired and invalid tokens fail without signing in
- Non-local return URLs are rejected or normalized to `/`

#### Manual Verification:

- Development login request writes a usable magic link to logs
- Consuming the logged link signs in and redirects to a local return URL or `/`
- Reusing the same link no longer signs in

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Login/Logout UI And Navigation

### Overview

Add the smallest user-facing account surface: a login page, signed-in nav state, and logout action. Keep account/profile and favorites UI for later changes.

### Changes Required:

#### 1. Login Page

**File**: `LinuxGameCompat/Components/Pages/Login.razor`

**Intent**: Let visitors request a passwordless sign-in link.

**Contract**: Route `/login`; accept an email address and optional local `returnUrl`; submit to the magic-link request endpoint; show a generic check-inbox confirmation. Do not reveal whether the email belongs to an existing member.

#### 2. Auth-Aware Routing And Imports

**File**: `LinuxGameCompat/Components/Routes.razor`

**Intent**: Make auth state available to routed components.

**Contract**: Use the standard Blazor authorization route view pattern while keeping anonymous routes available.

**File**: `LinuxGameCompat/Components/_Imports.razor`

**Intent**: Add common auth namespaces needed by Razor components.

**Contract**: Imports should support `AuthorizeView` and auth-state usage without forcing every component to add local `@using` directives.

#### 3. Navigation State And Logout

**File**: `LinuxGameCompat/Components/Layout/NavMenu.razor`

**Intent**: Show login when anonymous and signed-in/logout controls when authenticated.

**Contract**: Preserve the existing `Lookup` navigation item. Show the current email or concise member label for authenticated users. Logout submits via POST and returns home.

#### 4. Current Member Accessor

**File**: `LinuxGameCompat/Services/ICurrentMemberAccessor.cs`

**Intent**: Provide a stable service contract for future favorites work.

**Contract**: Expose a method or property that returns the current authenticated local member id and email when available, without requiring downstream features to parse claims directly.

### Success Criteria:

#### Automated Verification:

- Build passes: `dotnet build LinuxGameCompat.sln --no-restore`
- Auth UI changes compile with nullable warnings clean
- Existing lookup tests still pass

#### Manual Verification:

- Anonymous nav shows `Lookup` and `Login`
- Signed-in nav shows a member label and logout control
- Logout clears the session and returns to `/`
- Anonymous lookup and detail pages still work after login UI changes

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Verification And Handoff

### Overview

Complete focused test coverage and document the operational configuration needed for production use.

### Changes Required:

#### 1. Integration Tests

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Extend the existing PostgreSQL test fixture coverage to include member auth persistence and magic-link lifecycle.

**Contract**: Test migration shape, request creation, deferred member creation, successful consumption, consumed-token rejection, expired-token rejection, invalid-token rejection, and local return URL handling. Keep existing compatibility tests intact.

#### 2. Auth Configuration Documentation

**File**: `context/changes/passwordless-member-access/implementation-notes.md`

**Intent**: Record the required runtime configuration for production and local development.

**Contract**: Document SMTP-related configuration keys, public app base URL configuration, development log-link behavior, and manual smoke-test steps. Do not include secrets.

#### 3. Plan Handoff State

**File**: `context/changes/passwordless-member-access/change.md`

**Intent**: Keep the change identity aligned with the implementation lifecycle.

**Contract**: Leave `status: planned` until `/10x-implement` begins execution; implementation progress is tracked only in the `## Progress` section below.

### Success Criteria:

#### Automated Verification:

- `dotnet build LinuxGameCompat.sln --no-restore` passes
- `dotnet test LinuxGameCompat.sln --no-restore` passes
- New auth integration tests pass under Testcontainers PostgreSQL

#### Manual Verification:

- Login request, logged dev link consumption, signed-in nav, logout, reused link, expired link, and invalid link are smoke-tested
- Production SMTP/base URL configuration requirements are documented

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before marking the change implemented.

---

## Testing Strategy

### Unit Tests:

- Token hashing/verification behavior if factored into a pure helper.
- Return URL normalization if factored into a pure helper.
- Current member accessor behavior for authenticated and anonymous principals if practical without full server hosting.

### Integration Tests:

- EF Core migration creates Identity and magic-link tables.
- Requesting login stores a hashed one-time token and does not create a member.
- Consuming a valid token creates a member, marks the request consumed, and supports sign-in.
- Consuming the same token twice fails.
- Expired, malformed, and unknown tokens fail safely.
- Existing compatibility read-service tests continue to pass.

### Manual Testing Steps:

1. Start the app in Development with the normal local database.
2. Open `/login`, enter an email, and submit the form.
3. Copy the logged magic link from application logs and open it.
4. Confirm the nav shows the signed-in member state.
5. Search for a game from `/` and open a detail page while signed in.
6. Submit logout and confirm the nav returns to anonymous state.
7. Reopen the consumed link and confirm it no longer signs in.
8. Test an invalid token URL and confirm the app shows a safe failure path.

## Performance Considerations

Magic-link tables should be indexed by token hash and normalized email. Request lookups and consumption should be single-row operations. MVP does not implement request throttling; per-normalized-email throttling is a known follow-up before public or higher-volume launch. Broader abuse controls can wait until real usage exists.

## Known MVP Tradeoffs / Deferred Hardening

- Email scanners and security tools can prefetch `GET /auth/magic-link/consume` links, which may consume the one-time token before the user intentionally opens it. MVP accepts this risk for a simpler flow. Future hardening should make `GET` render a confirmation page and consume/sign in only on `POST` with antiforgery protection.
- Magic-link request throttling is deferred. The schema keeps request metadata suitable for future per-normalized-email throttling, but MVP implementation should not claim to enforce send limits.
- Full web-host auth endpoint tests are deferred. MVP automated coverage should focus on migrations and service/persistence behavior, with manual smoke testing covering routing, redirects, cookies, and real sign-in/logout behavior.

## Migration Notes

This change adds auth-related tables to the existing PostgreSQL database. No existing compatibility rows should be modified. The migration should be explicit and should not be applied automatically at app startup.

## References

- PRD access control: `context/foundation/prd.md`
- Roadmap F-02: `context/foundation/roadmap.md`
- Existing auth gap: `LinuxGameCompat/Program.cs`
- Existing database context: `LinuxGameCompat/Data/CompatibilityDbContext.cs`
- Existing lookup UI: `LinuxGameCompat/Components/Pages/Home.razor`
- Existing detail UI: `LinuxGameCompat/Components/Pages/GameDetail.razor`
- Microsoft Identity docs: `https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity`
- Microsoft Blazor auth state docs: `https://learn.microsoft.com/en-us/aspnet/core/blazor/security/authentication-state`
- Microsoft account confirmation/email docs: `https://learn.microsoft.com/en-us/aspnet/core/blazor/security/account-confirmation-and-password-recovery`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Identity Persistence And Auth Infrastructure

#### Automated

- [ ] 1.1 Build passes
- [ ] 1.2 Migration applies cleanly in PostgreSQL-backed tests
- [ ] 1.3 Existing compatibility read-service tests still pass

#### Manual

- [ ] 1.4 Anonymous `/` lookup remains reachable without signing in
- [ ] 1.5 Anonymous `/games/{slug}` detail remains reachable without signing in

### Phase 2: Magic-Link Request And Consumption Flow

#### Automated

- [ ] 2.1 Magic-link request stores a hashed token and does not create a member immediately
- [ ] 2.2 Valid magic-link consumption creates a member for a new email and marks the request consumed
- [ ] 2.3 Reusing a consumed token fails
- [ ] 2.4 Expired and invalid tokens fail without signing in
- [ ] 2.5 Non-local return URLs are rejected or normalized to `/`

#### Manual

- [ ] 2.6 Development login request writes a usable magic link to logs
- [ ] 2.7 Consuming the logged link signs in and redirects to a local return URL or `/`
- [ ] 2.8 Reusing the same link no longer signs in

### Phase 3: Login/Logout UI And Navigation

#### Automated

- [ ] 3.1 Build passes
- [ ] 3.2 Auth UI changes compile with nullable warnings clean
- [ ] 3.3 Existing lookup tests still pass

#### Manual

- [ ] 3.4 Anonymous nav shows `Lookup` and `Login`
- [ ] 3.5 Signed-in nav shows a member label and logout control
- [ ] 3.6 Logout clears the session and returns to `/`
- [ ] 3.7 Anonymous lookup and detail pages still work after login UI changes

### Phase 4: Verification And Handoff

#### Automated

- [ ] 4.1 `dotnet build LinuxGameCompat.sln --no-restore` passes
- [ ] 4.2 `dotnet test LinuxGameCompat.sln --no-restore` passes
- [ ] 4.3 New auth integration tests pass under Testcontainers PostgreSQL

#### Manual

- [ ] 4.4 Login request, logged dev link consumption, signed-in nav, logout, reused link, expired link, and invalid link are smoke-tested
- [ ] 4.5 Production SMTP/base URL configuration requirements are documented
