# Passwordless Member Access - Plan Brief

> Full plan: `context/changes/passwordless-member-access/plan.md`

## What & Why

Add passwordless member identity so the later favorites slice can attach saved games to a logged-in member. The PRD requires passwordless login for member features, with no account recovery in the MVP.

## Starting Point

The app already has anonymous lookup, game detail pages, EF Core persistence, PostgreSQL migrations, and Testcontainers-backed integration tests. It has no auth packages, Identity models, cookie auth, auth middleware, account routes, or auth-aware navigation.

## Desired End State

Visitors can request an email magic link, consume a valid one-time link, become signed in as a member, and sign out. Unknown emails auto-create a member only after valid link consumption. Future favorites work can read the current member id through a small service contract.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Passwordless mechanism | Email magic links | Familiar MVP flow and no app-managed passwords. |
| Auth stack | ASP.NET Core Identity | Uses maintained framework primitives for users, tokens, cookies, and EF storage. |
| Member key | Local Identity user id with normalized email login | Gives favorites a stable local foreign key while keeping email as the visible login identity. |
| Signup policy | Auto-create after valid link consumption | Matches the open visitor-to-member path without exposing account existence. |
| Link policy | 15 minutes, one use | Strong default for emailed bearer links. |
| Session policy | 30-day persistent cookie | Low friction for a favorites product users revisit occasionally. |
| Email delivery | SMTP abstraction plus dev logging fallback | Provider-neutral production path with simple local testing. |
| UI scope | Login/logout only | Keeps this foundation focused; favorites can define richer account UX later. |
| Testing level | Backend integration tests plus manual UI smoke | Matches current repo test patterns without adding browser test tooling. |

## Scope

**In scope:**

- ASP.NET Core Identity setup.
- Identity and magic-link database migration.
- Email magic-link request and consume flow.
- Auto-create member after verified link consumption.
- 30-day persistent cookie sign-in.
- SMTP sender abstraction and development logging sender.
- Minimal `/login`, magic-link consume, and logout surface.
- Auth-aware nav state.
- Current-member accessor for future favorites.
- PostgreSQL integration tests and manual smoke checklist.

**Out of scope:**

- Favorites.
- Account recovery.
- Password login.
- Profile/account management.
- Roles/admin features.
- Passkeys.
- External OAuth providers.
- Hosted auth service integration.
- Browser E2E test framework.

## Architecture / Approach

Extend the existing EF Core/PostgreSQL app database with ASP.NET Core Identity tables and an app-specific `MagicLinkRequest` table. A magic-link service creates hashed one-time tokens, sends provider-neutral email links, validates consumption, creates members on first verified sign-in, and issues the Identity cookie. Blazor receives auth state for navigation and future protected features, while anonymous lookup remains public.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Identity Persistence And Auth Infrastructure | Identity schema, app user, auth registration, middleware | Auth middleware ordering or migration shape could break existing public routes. |
| 2. Magic-Link Request And Consumption Flow | Secure request/send/consume/sign-in service and endpoints | Token replay, expired links, or open redirects must be handled correctly. |
| 3. Login/Logout UI And Navigation | Minimal login page, logout, signed-in nav, current-member accessor | UI must not imply profile/favorites features that do not exist yet. |
| 4. Verification And Handoff | Integration coverage and production config notes | Email configuration and manual smoke steps must be clear enough for deployment. |

**Prerequisites:** Existing PostgreSQL migration path and anonymous lookup implementation are present.
**Estimated effort:** About 2-3 focused implementation sessions across 4 phases.

## Open Risks & Assumptions

- Production SMTP provider and credentials are not chosen here; the app only defines a provider-neutral SMTP contract.
- Development uses logged magic links for smoke testing.
- Basic per-email request throttling is enough for MVP; broader abuse controls are future work.
- The implementation uses the same PostgreSQL database as compatibility data.
- No automatic startup migration is introduced.

## Success Criteria (Summary)

- A visitor can request and consume a one-use email magic link, then see signed-in nav state.
- A valid first sign-in creates a local member; invalid, expired, consumed, and unsafe-return links fail safely.
- Existing anonymous lookup and detail pages remain publicly accessible.
- `dotnet build LinuxGameCompat.sln --no-restore` and `dotnet test LinuxGameCompat.sln --no-restore` pass.
