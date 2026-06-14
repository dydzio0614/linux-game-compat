---
date: 2026-06-03T18:41:43+02:00
researcher: Codex
git_commit: a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4
branch: master
repository: LinuxGameCompat
topic: "Ground rollout Phase 1 auth and privacy regression floor risks #1 and #2"
tags: [research, codebase, auth, privacy, magic-link, identity, testing]
status: complete
last_updated: 2026-06-03
last_updated_by: Codex
---

# Research: Ground rollout Phase 1 auth and privacy regression floor risks #1 and #2

**Date**: 2026-06-03T18:41:43+02:00
**Researcher**: Codex
**Git Commit**: a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4
**Branch**: master
**Repository**: LinuxGameCompat

## Research Question

Ground rollout Phase 1 of `context/foundation/test-plan.md`.

Risks to verify: #1, #2.

Risk response guidance to verify, not blindly accept:
- Risk #1: prove invalid, expired, consumed, and replayed links do not sign in; request responses stay generic; unsafe return URLs do not redirect externally; challenge "happy-path login means auth is safe"; avoid happy-path-only auth tests, over-mocking identity/cookie behavior, and asserting implementation internals instead of sign-in outcome.
- Risk #2: prove stored, logged, sent, and failed-email artifacts do not expose raw tokens or more user-email/account information than required for operation; challenge "hashing the token is enough to cover privacy"; avoid copied production calculation as oracle, testing only success path, and ignoring failure logs.

Hot-spot directories that raised these risks: `LinuxGameCompat/Services`, `LinuxGameCompat/Program.cs`.

## Summary

Risk #1 is mostly grounded and partially covered already at the PostgreSQL-backed service integration layer. The real sign-in boundary is `MagicLinkService.ConsumeLoginLinkAsync`: it rejects missing, consumed, and expired token rows before sign-in, conditionally marks the row consumed, then signs in only after that persisted update succeeds. Existing tests cover hashed token storage, valid consume, consumed-token replay, expired and invalid tokens, and one obvious external return URL case.

The response guidance needs correction in three places. First, current request handling does not branch on whether an account already exists, so "reveals whether an email exists" is over-broad if it means account enumeration. Second, request failures are not fully generic across invalid email or send failure because the endpoint redirects to `/login?requestFailed=1`; that discloses request validity/failure class, not existing-account state. Third, the remaining redirect risk is more specific than "external URL": the service rejects absolute URLs and `//host` values, but does not explicitly reject slash/backslash forms such as `/\evil.example`; whether that becomes external depends on browser/proxy normalization, so it deserves a focused test or stricter normalization.

Risk #2 is real and only partially covered. The database stores token hashes, not raw tokens, and failed email sends remove saved requests. However, raw tokens necessarily cross the email boundary, development logging intentionally logs the full login link, and failed-send logs include the normalized email. The guidance "hashing the token is enough" is correctly challenged: hashing protects the token-at-rest boundary only, not logs, email artifacts, retained email identifiers, IP address, or user agent metadata.

The cheapest useful layer remains service/integration tests first. Add focused tests around existing-vs-new email equivalence, failure-log content, privacy boundaries for captured sender artifacts, and the slash/backslash return URL edge. Endpoint/cookie tests would be useful later for actual HTTP cookie issuance and browser redirect behavior, but the current project has no `WebApplicationFactory`, `TestServer`, Playwright, or bUnit infrastructure.

## Detailed Findings

### Risk #1: Magic-Link Sign-In Safety

The live auth entry points are minimal endpoints in `Program.cs`: request at `POST /auth/magic-link/request`, consume at `GET /auth/magic-link/consume`, and logout at `POST /logout` ([Program.cs:71](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L71), [Program.cs:94](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L94), [Program.cs:102](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L102)). The request endpoint explicitly disables antiforgery ([Program.cs:93](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L93)); logout requires authorization and antiforgery metadata ([Program.cs:106](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L106)).

Token generation and persistence happen in `MagicLinkService.RequestLoginLinkAsync`. The service generates a raw random token, stores only `HashToken(token)`, sets a 15-minute expiry, normalizes the return URL, and stores request IP/user-agent metadata ([MagicLinkService.cs:44](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L44), [MagicLinkService.cs:49](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L49), [MagicLinkService.cs:51](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L51), [MagicLinkService.cs:52](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L52)). Relevant short quote: `TokenHash = HashToken(token)`.

Invalid, consumed, and expired tokens do not reach sign-in in the service path. Consumption loads by token hash, rejects missing rows, rows with `ConsumedAt`, and rows whose `ExpiresAt <= now` ([MagicLinkService.cs:89](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L89), [MagicLinkService.cs:94](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L94)). Relevant short quote: `request is null || request.ConsumedAt is not null || request.ExpiresAt <= now`.

Replay/race resistance is stronger than a simple in-memory check. The service conditionally updates the row only when `ConsumedAt == null` and `ExpiresAt > now`, then requires exactly one updated row before user creation or sign-in ([MagicLinkService.cs:99](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L99), [MagicLinkService.cs:108](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L108)). Sign-in happens only after that update and after user lookup/creation ([MagicLinkService.cs:113](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L113), [MagicLinkService.cs:130](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L130)). Relevant short quote: `SignInAsync(user, isPersistent: true)`.

User creation is deferred until a valid token is consumed. Request-time code does not call `FindByEmailAsync`; consume-time code does, then creates a member only if needed ([MagicLinkService.cs:113](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L113), [MagicLinkService.cs:116](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L116)). This corrects the test-plan wording: existing-account enumeration is not visible in current request behavior. The useful test is an equivalence test for existing and new syntactically valid email addresses, not just another happy path.

Failed consumption returns one generic failure route from the service: `/login?failed=1` ([MagicLinkService.cs:135](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L135)). The UI renders a generic failed-link message ([Login.razor:22](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Components/Pages/Login.razor#L22)). Request failures are less generic: invalid email or send failure returns `Accepted: false`, and the endpoint redirects to `/login?requestFailed=1` ([MagicLinkService.cs:32](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L32), [Program.cs:92](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L92), [Login.razor:29](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Components/Pages/Login.razor#L29)). This is not an existing-account leak, but it is a distinct user-visible request failure signal.

Return URL normalization is duplicated in the login component and service. The service boundary matters most because direct POSTs can bypass the UI hidden-field normalization. It only accepts relative URIs whose string form starts with `/`, does not start with `//`, and is within the max length ([MagicLinkService.cs:178](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L178), [MagicLinkService.cs:186](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L186), [Login.razor:74](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Components/Pages/Login.razor#L74)). It blocks `https://evil.example` and `//evil.example`; it does not explicitly reject `/\evil.example` or encoded backslash variants. That is the most concrete untested redirect edge.

### Risk #2: Token, Email, and Auth-Material Privacy

The persistence boundary stores `NormalizedEmail`, `TokenHash`, `ExpiresAt`, `ConsumedAt`, `ReturnUrl`, `CreatedAt`, `RequestIpAddress`, and `UserAgent` ([MagicLinkRequest.cs:7](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Data/MagicLinkRequest.cs#L7), [MagicLinkRequest.cs:19](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Data/MagicLinkRequest.cs#L19)). EF makes `TokenHash` unique and indexes normalized email and expiry ([CompatibilityDbContext.cs:83](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Data/CompatibilityDbContext.cs#L83), [CompatibilityDbContext.cs:90](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Data/CompatibilityDbContext.cs#L90), [CompatibilityDbContext.cs:91](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Data/CompatibilityDbContext.cs#L91)). No pruning or retention path was found. This is intentional metadata for future throttling/audit, but it means privacy tests should not claim only token hashing matters.

Raw tokens cross the email boundary by design. `BuildLoginLink` adds the raw token query parameter, and `SmtpAuthEmailSender` places the login link in the email body ([MagicLinkService.cs:151](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L151), [MagicLinkService.cs:156](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L156), [SmtpAuthEmailSender.cs:24](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/SmtpAuthEmailSender.cs#L24), [SmtpAuthEmailSender.cs:27](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/SmtpAuthEmailSender.cs#L27)). A regression floor should assert the raw token is confined to the expected outbound sender artifact and absent from persistence and failure logs.

Development logging intentionally logs the email address and full login link, including the raw token ([LoggingAuthEmailSender.cs:7](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/LoggingAuthEmailSender.cs#L7), [LoggingAuthEmailSender.cs:9](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/LoggingAuthEmailSender.cs#L9)). `Program.cs` registers that sender only in Development and uses SMTP otherwise ([Program.cs:44](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L44), [Program.cs:50](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L50)). This directly conflicts with a blanket "logged artifacts do not expose raw tokens" claim. The test guidance should either scope this as an accepted Development-only manual-smoke tradeoff or change the sender to log a redacted link.

Email-send failure removes the saved request before returning failure, so it does not leave an active token row behind ([MagicLinkService.cs:69](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L69), [MagicLinkService.cs:71](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L71)). It logs the normalized email address but not the raw token or login link ([MagicLinkService.cs:73](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L73)). This is acceptable if normalized email is considered required operational context for diagnosing send failures; if not, the log message should be reduced or keyed by a non-identifying correlation id.

The public-base URL host-header leak identified in archived implementation review is fixed in live code. Outside Development, `AuthPublicBaseUrl` must be configured and HTTPS; request host fallback is Development-only ([AuthPublicBaseUriResolver.cs:7](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/AuthPublicBaseUriResolver.cs#L7), [AuthPublicBaseUriResolver.cs:15](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/AuthPublicBaseUriResolver.cs#L15), [AuthPublicBaseUriResolver.cs:18](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/AuthPublicBaseUriResolver.cs#L18)).

### Existing Tests

Auth/privacy tests live inside `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`, not in a dedicated auth test file. The test project uses xUnit and Testcontainers PostgreSQL ([LinuxGameCompat.Tests.csproj:10](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/LinuxGameCompat.Tests.csproj#L10), [PostgreSqlCompatibilityTests.cs:13](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L13)). `CreateAuthScope` wires EF, Identity, `SignInManager`, a fake email sender, and `TimeProvider` against the PostgreSQL container ([PostgreSqlCompatibilityTests.cs:572](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L572)).

Covered now:
- Auth schema exists, including `MagicLinkRequests`, `TokenHash`, `ConsumedAt`, request IP, user agent, and indexes ([PostgreSqlCompatibilityTests.cs:62](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L62)).
- Public base URI production safety rejects missing/non-HTTPS config and allows configured HTTPS ([PostgreSqlCompatibilityTests.cs:282](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L282), [PostgreSqlCompatibilityTests.cs:305](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L305)).
- Request stores a hashed token, keeps raw token out of the row, preserves local return URL and metadata, and does not create a member immediately ([PostgreSqlCompatibilityTests.cs:337](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L337)).
- Invalid email does not persist or send ([PostgreSqlCompatibilityTests.cs:363](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L363)).
- Overlong return URL normalizes to root ([PostgreSqlCompatibilityTests.cs:384](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L384)).
- Email-send failure removes the saved request ([PostgreSqlCompatibilityTests.cs:404](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L404)).
- Valid consume creates the member and marks the request consumed ([PostgreSqlCompatibilityTests.cs:425](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L425)).
- Consumed-token replay fails ([PostgreSqlCompatibilityTests.cs:451](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L451)).
- Expired and invalid tokens fail without creating a member ([PostgreSqlCompatibilityTests.cs:472](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L472)).
- Obvious absolute external return URL normalizes to root ([PostgreSqlCompatibilityTests.cs:499](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L499)).

Missing or thin:
- No existing-vs-new email equivalence test proves account existence is not disclosed for syntactically valid addresses.
- No logger-content assertion proves failure logs omit raw tokens/login links.
- No test scopes or challenges `LoggingAuthEmailSender` leaking full links in Development.
- No slash/backslash or encoded-backslash unsafe return URL test.
- No endpoint-level HTTP test proves cookie issuance, `Location` headers, actual redirect behavior, or login UI request messages. This is a known higher-cost gap, not the cheapest first layer.

## Code References

- [LinuxGameCompat/Program.cs:71](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L71) - Magic-link request endpoint.
- [LinuxGameCompat/Program.cs:94](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Program.cs#L94) - Magic-link consume endpoint.
- [LinuxGameCompat/Services/MagicLinkService.cs:28](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L28) - Request flow.
- [LinuxGameCompat/Services/MagicLinkService.cs:80](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L80) - Consume flow and sign-in boundary.
- [LinuxGameCompat/Services/MagicLinkService.cs:178](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/MagicLinkService.cs#L178) - Service-side return URL normalization.
- [LinuxGameCompat/Services/LoggingAuthEmailSender.cs:9](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/LoggingAuthEmailSender.cs#L9) - Development full-link logging.
- [LinuxGameCompat/Services/SmtpAuthEmailSender.cs:24](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Services/SmtpAuthEmailSender.cs#L24) - Production email recipient and body boundary.
- [LinuxGameCompat/Data/MagicLinkRequest.cs:7](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat/Data/MagicLinkRequest.cs#L7) - Persisted email/token/request metadata.
- [LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs:337](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L337) - Existing token hashing/deferred member creation test.
- [LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs:451](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L451) - Existing consumed-token replay test.
- [LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs:472](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L472) - Existing expired/invalid token test.
- [LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs:499](https://github.com/dydzio0614/linux-game-compat/blob/a80aeb3cfae7c8fcd6afbce439c8b327d5ee0ff4/LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs#L499) - Existing obvious external return URL test.

## Architecture Insights

The core auth logic is service-owned, with minimal endpoints delegating to `IMagicLinkService`. That makes PostgreSQL-backed service integration tests the right first layer for token lifecycle, persisted request state, deferred member creation, and return URL normalization.

The test plan correctly warns against happy-path-only auth tests. Live code has good service-level coverage for invalid, expired, and consumed token branches, so the next useful tests should target gaps, not duplicate the happy path.

The privacy boundary is split across persistence, email sender, and logger behavior. Tests that only assert `TokenHash` is not equal to the raw token are necessary but insufficient. The raw token must exist in the outbound email link; the regression floor should prove it does not also appear in saved rows or failure logs.

`LoggingAuthEmailSender` is the main guidance conflict. It is an intentional Development-only implementation for manual smoke testing, but it means a broad "logged artifacts never expose raw tokens" requirement is false today.

Endpoint/cookie behavior remains outside the current cheapest layer. Service tests can prove `ConsumeLoginLinkAsync` returns `Succeeded = false` for invalid states and calls Identity sign-in on success through `SignInManager`, but they cannot prove the HTTP response set the actual auth cookie or that a browser treats a borderline `Location` value safely.

## Historical Context

- `context/archive/2026-05-31-passwordless-member-access/plan.md` planned the current service contract: random token, stored hash only, 15-minute lifetime, one-use links, unknown-user creation after valid consumption, generic failures, and local return URLs.
- `context/archive/2026-05-31-passwordless-member-access/plan.md` deliberately deferred full web-host auth endpoint tests in favor of service/persistence coverage plus manual smoke for routing, redirects, cookies, and real sign-in/logout behavior.
- `context/archive/2026-05-31-passwordless-member-access/reviews/plan-review.md` flagged `GET /auth/magic-link/consume` as vulnerable to email scanner prefetch. That remains true: scanners can consume a token before the user intentionally clicks it, although this is a reliability/user-experience risk more than an account-takeover sign-in risk for this phase.
- `context/archive/2026-05-31-passwordless-member-access/reviews/impl-review.md` found host-header token leak, missing logout antiforgery enforcement, and send-failure active-token cleanup. Live code now fixes those with production HTTPS `Auth:PublicBaseUrl`, explicit logout antiforgery metadata, and saved-request removal on send failure.
- `context/archive/2026-05-31-passwordless-member-access/follow-ups/review-fixes.md` keeps request throttling as a follow-up before public or higher-volume launch. It is adjacent to auth abuse and table growth, but not the same as risks #1 and #2 unless this phase chooses to broaden scope.

## Related Research

No prior `research.md` artifact for this exact phase was found under `context/changes/testing-auth-privacy-regression-floor/`.

Related archived artifacts:
- `context/archive/2026-05-31-passwordless-member-access/plan.md`
- `context/archive/2026-05-31-passwordless-member-access/implementation-notes.md`
- `context/archive/2026-05-31-passwordless-member-access/reviews/plan-review.md`
- `context/archive/2026-05-31-passwordless-member-access/reviews/impl-review.md`
- `context/archive/2026-05-31-passwordless-member-access/follow-ups/review-fixes.md`

## Open Questions

- Should Development logging of full magic links remain an accepted local-only tradeoff, or should it be redacted so the regression floor can say no logs contain raw tokens?
- Is normalized email in failed-send logs considered required operational context, or should failure logs avoid email identifiers?
- Should slash/backslash return URLs be rejected in service normalization before adding tests, or should a test first document current behavior as a failing regression target?
- Does Phase 1 stay at service/integration level, or should it add a minimal HTTP host test package now to prove actual cookies and `Location` headers?
