# Development Magic Link Shortcut - Plan Brief

> Full plan: `context/changes/development-magic-link-shortcut/plan.md`

## What & Why

Add an explicitly configurable shortcut that shows generated passwordless magic links in the login UI. This lets local and hosted test deployments exercise authenticated features without SMTP, which is important because cheap hosting tiers may block SMTP and a suitable HTTPS email provider is not yet chosen.

## Starting Point

Passwordless auth already works through `IMagicLinkService`, `/auth/magic-link/request`, `/auth/magic-link/consume`, and `/login`. Development currently logs full magic links, but the frontend cannot show them because `MagicLinkRequestResult` only reports `Accepted`.

## Desired End State

With `Auth:ShowMagicLinksInFrontend=false` or absent, login behaves exactly as it does today. With `Auth:ShowMagicLinksInFrontend=true`, a successful login request shows the generated one-use magic link inside the `/login` success panel with a clear test-only warning; clicking it signs in through the normal consume flow.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Enablement | `Auth:ShowMagicLinksInFrontend` explicit boolean | Simple to set in local or hosted app configuration and clear enough for MVP. |
| Environment policy | Allow in any environment by explicit config | Hosted testers need the shortcut even when the app is not running as `Development`. |
| Production guardrail | Loud UI warning, no startup block | Preserves deliberate demo/test flexibility while making the risk visible. |
| UI surface | Existing login success panel | Keeps the shortcut in the flow where testers need it. |
| Redirect handoff | Cookie-backed TempData | Avoids putting the raw token in the redirect query string or browser history. |
| Logging | Keep Development full-link logging as-is | Preserves current manual-smoke fallback behavior. |
| Testing level | Service/unit coverage plus manual UI smoke | Matches current repo test style without adding browser tooling for a dev/test-only feature. |

## Scope

**In scope:**

- Config flag for frontend magic-link display.
- Optional generated-link result from the magic-link request service.
- TempData/cookie-backed one-time handoff from request endpoint to login page.
- Login success UI warning and link display.
- Focused auth regression tests.
- README note for hosted-test configuration and risk.

**Out of scope:**

- New auth mechanism or account features.
- Database migration.
- Recent-link archive or diagnostics page.
- SMTP provider selection.
- Browser automation.
- Disabling existing Development full-link logging.

## Architecture / Approach

The existing auth flow remains authoritative: token generation, hashing, send failure cleanup, 15-minute expiry, one-use consumption, and local return URL normalization stay in `MagicLinkService`. The request endpoint opts into receiving the generated link only when `Auth:ShowMagicLinksInFrontend` is true, stores it briefly for the redirected login page, and the login page renders it with warning copy.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Shortcut Contract And Configuration | Config flag, optional result link, TempData handoff | Accidentally exposing raw links when the flag is disabled |
| 2. Login UI Display And Styling | Inline warning and generated link in login success state | Long token URLs overflow or look like normal production UX |
| 3. Verification And Handoff | Focused tests and README configuration note | Missing a failed-request or disabled-config regression |

**Prerequisites:** Existing passwordless auth and auth/privacy regression tests are present.
**Estimated effort:** About 1-2 focused implementation sessions across 3 phases.

## Open Risks & Assumptions

- Enabling `Auth:ShowMagicLinksInFrontend=true` exposes bearer login links to anyone who can see the login success page.
- The shortcut is intentionally allowed in any environment by explicit config.
- Operators must keep the flag disabled for real public production traffic unless they deliberately accept the test/demo risk.
- TempData/cookie handoff is suitable for this single-link display; no multi-instance link archive is needed.

## Success Criteria (Summary)

- Disabled config preserves the current email-only login success UX.
- Enabled config shows a generated link inline and that link signs the tester in through the normal consume flow.
- Auth/privacy regression tests continue to pass, and new tests cover default/no-link and failed-request/no-link behavior.
