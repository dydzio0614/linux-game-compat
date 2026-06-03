# Auth And Privacy Regression Floor Implementation Plan

## Overview

Roll out Phase 1 of `context/foundation/test-plan.md` by adding a focused
regression floor for risks #1 and #2: passwordless magic-link auth safety and
token/email privacy boundaries. The phase stays at the requested service and
PostgreSQL integration-test layer because research found that layer already
observes the token lifecycle, persisted request state, Identity-backed sign-in
boundary, return URL normalization, sender boundary, and failure cleanup.

## Current State Analysis

The app already has passwordless member access backed by ASP.NET Core Identity,
PostgreSQL, `MagicLinkRequest` persistence, a magic-link service, a development
logging sender, and an SMTP sender. Existing tests in
`LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs` cover hashed token
storage, deferred member creation, valid consumption, consumed-token replay,
expired and invalid tokens, failed-send cleanup, and one obvious absolute
external return URL case.

Research found the remaining useful Phase 1 gaps are narrower than a full auth
test suite: existing-vs-new valid email request equivalence, unsafe
slash/backslash return URL forms, failure-log privacy, production email content
composition, and explicit handling of the Development full-link logging
exception.

## Desired End State

The test suite contains a dedicated auth/privacy regression floor that proves
the intended service-level behavior without adding TestServer or browser smoke
in this phase. Invalid, expired, consumed, and replayed links do not sign in;
valid existing and new email requests have equivalent service-visible outcomes;
unsafe return URL edge cases normalize to `/`; raw tokens stay out of
persistence and failure logs; failed sends leave no active token rows; and
production email composition contains only the intended login link.

### Key Discoveries:

- `context/changes/testing-auth-privacy-regression-floor/research.md` found
  that risk #1 is already partially covered but still needs existing-vs-new
  valid email equivalence and slash/backslash redirect-edge tests.
- `LinuxGameCompat/Services/MagicLinkService.cs:80` is the real consume and
  sign-in boundary; it rejects missing, consumed, and expired requests before
  the call to Identity sign-in.
- `LinuxGameCompat/Services/MagicLinkService.cs:178` and
  `LinuxGameCompat/Components/Pages/Login.razor:74` duplicate local return URL
  normalization, so hardening should avoid divergent service/UI behavior.
- `LinuxGameCompat/Services/LoggingAuthEmailSender.cs:9` intentionally logs
  full login links in Development; this remains an accepted local-only manual
  smoke tradeoff for this phase.
- `LinuxGameCompat/Properties/AssemblyInfo.cs` already grants
  `InternalsVisibleTo("LinuxGameCompat.Tests")`, so small internal helper seams
  can be tested without making them public.
- Baseline on 2026-06-03: `dotnet test LinuxGameCompat.sln --no-restore`
  passed 39/39.

## What We're NOT Doing

- No TestServer, browser smoke, Playwright, bUnit, or broad e2e coverage.
- No proof of actual auth-cookie issuance or browser `Location` handling in
  this phase; those remain Phase 3 browser-smoke concerns.
- No request throttling or abuse-rate limiting.
- No favorites, member-owned data tests, account recovery, password login, or
  profile management.
- No real SMTP provider integration or fake SMTP server.
- No blanket "no logs contain raw tokens" rule while Development logging
  intentionally emits full links.
- No claim that the current request-failure UI is account enumeration without
  evidence.

## Implementation Approach

Keep the cheapest useful layer: xUnit plus PostgreSQL Testcontainers plus real
EF Core and Identity wiring. Extract auth test support out of the broad
compatibility test class, add a dedicated auth/privacy test file, harden shared
return URL normalization, add the smallest internal production-email
composition seam needed for deterministic inspection, and update the test-plan
cookbook after the patterns land.

## Critical Implementation Details

### Timing & lifecycle

Return URL hardening should be shared by the service boundary and login page
hidden-field normalization so direct posts and UI-generated requests follow the
same local-url contract.

### Debug & observability

Failure-log privacy tests should assert absence of the raw token, `token=`, and
full login links while allowing the normalized email address because that was
chosen as intended operational context for send-failure diagnostics.

## Phase 1: Extract Auth Test Support

### Overview

Move the shared PostgreSQL/auth test infrastructure into focused test-support
types so the new risk #1/#2 tests are readable and do not further inflate the
broad compatibility test file.

### Changes Required:

#### 1. PostgreSQL Fixture Ownership

**File**: `LinuxGameCompat.Tests/PostgreSqlFixture.cs`

**Intent**: Extract the existing PostgreSQL Testcontainers fixture into its own
test-support file while preserving current migration setup and disposal
behavior.

**Contract**: Keep the public test-facing fixture contract equivalent to the
current `PostgreSqlFixture`: expose `ConnectionString`, expose configured
`DbContextOptions<CompatibilityDbContext>`, migrate during initialization, and
create `CompatibilityDbContext` instances for tests.

#### 2. Auth Test Harness

**File**: `LinuxGameCompat.Tests/AuthTestHarness.cs`

**Intent**: Extract reusable auth service wiring, fake sender behavior, mutable
time, token extraction, and optional log capture for auth/privacy tests.

**Contract**: The harness must wire real EF Core, Identity,
`SignInManager<ApplicationUser>`, `IHttpContextAccessor`, `IMagicLinkService`,
`IAuthEmailSender`, and `TimeProvider` against the PostgreSQL fixture. It must
avoid mocking Identity sign-in internals and should expose only behavior-level
helpers that tests need.

#### 3. Compatibility Test Cleanup

**File**: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`

**Intent**: Remove duplicated fixture and auth helper definitions after the
shared support files exist, keeping existing compatibility and current auth
tests passing during the transition.

**Contract**: Do not change the meaning of existing tests during extraction.
This phase is a structural move only.

### Risk Trace:

- Behavior asserted: Existing tests continue to run through real PostgreSQL,
  EF Core, and Identity services.
- Regression caught: Auth/privacy coverage becoming unreadable or duplicated
  across unrelated test classes.
- Research source: `research.md` "Existing Tests"; current helpers at
  `PostgreSqlCompatibilityTests.cs:572`.
- Edge/error/boundary case: Existing auth tests must still cover invalid,
  expired, consumed, and non-local return URL behavior after extraction.
- Anti-pattern avoided: Over-mocking Identity/cookie behavior or duplicating
  Testcontainers setup.

### Success Criteria:

#### Automated Verification:

- `dotnet build LinuxGameCompat.sln --no-restore` passes after test-support extraction
- `dotnet test LinuxGameCompat.sln --no-restore` passes with the existing 39 tests
- Existing auth tests still use real PostgreSQL-backed Identity service wiring

#### Manual Verification:

- Review confirms no production app behavior changed in this structural phase

**Implementation Note**: After completing this phase and all automated
verification passes, pause here for manual confirmation from the human that the
manual testing was successful before proceeding to the next phase. Phase blocks
use plain bullets; the corresponding checkbox state lives in the `## Progress`
section at the bottom of the plan.

---

## Phase 2: Auth Safety Regressions And Return URL Hardening

### Overview

Add the missing risk #1 coverage and harden return URL normalization so
slash/backslash edge cases cannot become external redirects after browser or
proxy normalization.

### Changes Required:

#### 1. Shared Local Return URL Normalizer

**File**: `LinuxGameCompat/Services/LocalReturnUrlNormalizer.cs`

**Intent**: Replace duplicated local return URL normalization with a single
internal service/UI helper contract.

**Contract**: Return `/` for null, blank, absolute, protocol-relative,
overlong, raw backslash, encoded backslash, and encoded slash-host forms. Keep
ordinary local paths such as `/games/baldurs-gate-3` valid. The helper remains
internal and testable through the existing `InternalsVisibleTo` assembly
attribute.

#### 2. Service And UI Normalization Call Sites

**File**: `LinuxGameCompat/Services/MagicLinkService.cs`

**Intent**: Use the shared normalizer when persisting request return URLs and
when returning the post-consume redirect.

**Contract**: Preserve the existing successful local redirect behavior and
failure redirect behavior while rejecting the newly covered unsafe edge cases.

**File**: `LinuxGameCompat/Components/Pages/Login.razor`

**Intent**: Use the same shared normalizer for the hidden `returnUrl` field on
the login form.

**Contract**: The hidden field should not pass an unsafe value to the auth
request endpoint, but the service remains the authoritative direct-post
boundary.

#### 3. Dedicated Auth Safety Tests

**File**: `LinuxGameCompat.Tests/AuthPrivacyRegressionTests.cs`

**Intent**: Add behavior-level regression tests for existing-vs-new email
equivalence, invalid/expired/consumed/replayed token failures, and unsafe
return URL normalization.

**Contract**: Tests should assert service-visible outcomes and persisted state,
not private implementation details. Existing and new syntactically valid email
requests both return accepted outcomes and send one link. Invalid, expired,
consumed, and replayed consumes return failed results and do not create or
advance unintended members. Unsafe return URL cases normalize to `/`; normal
local paths remain intact.

### Risk Trace:

- Behavior asserted: Invalid, expired, consumed, and replayed links fail;
  valid existing and new email requests are equivalent; unsafe return URLs do
  not redirect externally.
- Regression caught: Happy-path auth masking unsafe token lifecycle, account
  state signaling, or open-redirect edge drift.
- Research source: `research.md` risk #1 findings and `test-plan.md` risk #1
  response guidance.
- Edge/error/boundary case: `/\evil.example`, `/%5Cevil.example`,
  `/%2Fevil.example`, `//evil.example`, `https://evil.example`, overlong
  return URL, expired token, consumed token replay, unknown token.
- Anti-pattern avoided: Happy-path-only auth tests, private hash-oracle
  assertions, and treating current request-failure UI as account enumeration
  without evidence.

### Success Criteria:

#### Automated Verification:

- `dotnet build LinuxGameCompat.sln --no-restore` passes after shared normalization changes
- `dotnet test LinuxGameCompat.sln --no-restore` passes with new auth safety tests
- Existing-vs-new valid email requests have equivalent accepted service outcomes
- Invalid, expired, consumed, and replayed consume attempts do not create or sign in unintended members
- Raw and encoded slash/backslash external-looking return URL cases normalize to `/`
- Ordinary local return URLs still round-trip through request and consume behavior

#### Manual Verification:

- Review confirms return URL hardening is shared by service and login UI normalization

**Implementation Note**: After completing this phase and all automated
verification passes, pause here for manual confirmation from the human that the
manual testing was successful before proceeding to the next phase.

---

## Phase 3: Privacy Boundary Regressions

### Overview

Add the missing risk #2 tests around token persistence, failure logging,
production email composition, failed-send cleanup, retained request metadata,
and the accepted Development logging exception.

### Changes Required:

#### 1. Production Email Composition Seam

**File**: `LinuxGameCompat/Services/SmtpAuthEmailSender.cs`

**Intent**: Make production email message composition inspectable without
sending mail through a real or fake SMTP server.

**Contract**: Add the smallest internal composition helper needed by tests. It
must preserve the existing public `IAuthEmailSender.SendLoginLinkAsync`
contract and keep runtime SMTP send behavior unchanged.

#### 2. Privacy Boundary Tests

**File**: `LinuxGameCompat.Tests/AuthPrivacyRegressionTests.cs`

**Intent**: Prove raw tokens stay out of persistence and failure logs, failed
sends do not leave active token rows, and production email composition contains
only the intended login link.

**Contract**: Use fake senders and captured logs at the service boundary. A
throwing sender may capture the generated login link before throwing so tests
can assert that the raw token and `token=` do not appear in failure logs while
the saved request is removed. Persistence tests should assert the stored token
is a hash and not the raw token without reimplementing the production hash as
the oracle.

#### 3. Development Logging Exception Test Or Documentation Guard

**File**: `LinuxGameCompat.Tests/AuthPrivacyRegressionTests.cs`

**Intent**: Encode the chosen policy that Development full-link logging remains
an accepted local-only manual-smoke exception.

**Contract**: Either a tiny test names the exception explicitly around
`LoggingAuthEmailSender`, or the cookbook update names it as a documented
exception. Do not add a blanket assertion that no logs ever contain raw tokens.

### Risk Trace:

- Behavior asserted: Raw tokens are absent from persistence and failure logs;
  failed sends remove saved requests; production email composition contains the
  intended login link; Development full-link logging is an explicit exception.
- Regression caught: Token leakage outside the intended sender boundary or
  accidental active-token retention after send failure.
- Research source: `research.md` risk #2 findings and `test-plan.md` risk #2
  response guidance.
- Edge/error/boundary case: Sender throws after receiving the raw-link artifact;
  failure log is inspected; SMTP composition is inspected without network I/O;
  normalized email remains allowed in failure logs.
- Anti-pattern avoided: Copied production hash calculations as test oracles,
  success-path-only privacy checks, ignoring failure logs, and broad log
  assertions contradicted by Development logging.

### Success Criteria:

#### Automated Verification:

- `dotnet build LinuxGameCompat.sln --no-restore` passes after sender composition seam changes
- `dotnet test LinuxGameCompat.sln --no-restore` passes with new privacy boundary tests
- Raw token is absent from saved `MagicLinkRequest` rows
- Raw token, `token=`, and full login links are absent from send-failure warning logs
- Failed sends leave no active magic-link request row
- Production email composition includes the intended login link and no extra auth material
- Development full-link logging is covered as an explicit local-only exception, not as a privacy failure

#### Manual Verification:

- Review confirms the privacy assertions match the accepted policy: normalized email is allowed in failed-send logs, Development full-link logging is an exception

**Implementation Note**: After completing this phase and all automated
verification passes, pause here for manual confirmation from the human that the
manual testing was successful before proceeding to the next phase.

---

## Phase 4: Cookbook And Handoff Gates

### Overview

Update the foundation test plan cookbook with the patterns shipped in Phase 1
so future auth/privacy tests follow the same cost x signal decisions and do not
reintroduce the avoided anti-patterns.

### Changes Required:

#### 1. Auth/Privacy Cookbook

**File**: `context/foundation/test-plan.md`

**Intent**: Replace the §6.1 placeholder with the shipped auth/privacy
regression pattern.

**Contract**: Document that auth/privacy regression tests use service +
PostgreSQL integration tests first; use real EF Core and Identity wiring; use
fake sender/logger boundaries; cover request equivalence, token lifecycle,
return URL hardening, persistence privacy, failure-log privacy, failed-send
cleanup, and the Development full-link logging exception.

#### 2. Per-Rollout Phase Notes

**File**: `context/foundation/test-plan.md`

**Intent**: Update §6.6 with 2-3 lines recording the Phase 1 cookbook pattern
and the cheapest useful layer chosen.

**Contract**: Date the AI-native guidance as 2026-06-03: no AI-native testing
layer is recommended for this phase because deterministic xUnit/Testcontainers
coverage is cheaper and directly tied to risks #1 and #2.

#### 3. Change Handoff State

**File**: `context/changes/testing-auth-privacy-regression-floor/change.md`

**Intent**: Keep lifecycle metadata aligned with execution state.

**Contract**: Leave `status: planned` until `/10x-implement` begins. Execution
progress is tracked only in the `## Progress` section below.

### Risk Trace:

- Behavior asserted: The shipped test patterns are discoverable from the
  foundation test plan before the next rollout phase starts.
- Regression caught: Future contributors adding expensive browser tests,
  brittle implementation mirrors, or blanket privacy assertions for risks #1
  and #2.
- Research source: `test-plan.md` §6 placeholders and risk response guidance.
- Edge/error/boundary case: Cookbook must explicitly mention the Development
  logging exception and service/integration-only phase boundary.
- Anti-pattern avoided: Leaving shipped patterns tribal, implying AI-native
  review is needed, or making Phase 1 look like an e2e requirement.

### Success Criteria:

#### Automated Verification:

- `dotnet test LinuxGameCompat.sln --no-restore` passes after cookbook updates
- `context/foundation/test-plan.md` §6.1 documents auth/privacy regression test patterns
- `context/foundation/test-plan.md` §6.6 records Phase 1 notes and 2026-06-03 AI-native guidance

#### Manual Verification:

- Review confirms the cookbook matches the implemented tests and accepted scope boundaries
- Review confirms `context/changes/testing-auth-privacy-regression-floor/change.md` remains `status: planned` until implementation begins

**Implementation Note**: After completing this phase and all automated
verification passes, pause here for manual confirmation from the human that the
manual testing was successful before marking the change implemented.

---

## Testing Strategy

### Unit Tests:

- Internal local return URL normalizer cases for valid local paths, absolute
  URLs, protocol-relative URLs, raw backslash host-like forms, encoded
  backslash forms, encoded slash-host forms, blank values, and overlong values.
- Internal production email composition helper, if introduced, to inspect
  subject/body/recipient without network I/O.
- Development logging exception test only if it provides clearer policy than a
  cookbook-only documentation guard.

### Integration Tests:

- PostgreSQL-backed auth request and consume behavior using real Identity
  service wiring.
- Existing-vs-new syntactically valid email request equivalence.
- Invalid, expired, consumed, and replayed token failure outcomes.
- Raw token absence from persistence.
- Send-failure cleanup and failure-log privacy.
- Safe and unsafe return URL behavior through request and consume flow.

### Manual Testing Steps:

1. Review the extracted test support to confirm it is structural and does not
   change production behavior.
2. Review the shared return URL normalizer call sites in service and login UI.
3. Review failure-log privacy expectations against the accepted policy:
   normalized email allowed, raw token and full link forbidden.
4. Review `context/foundation/test-plan.md` §6 updates for future-agent clarity.

## Performance Considerations

No runtime performance change is expected beyond a small shared normalization
helper. Integration tests continue to use the existing PostgreSQL Testcontainers
fixture; extracting test support should avoid adding another container.

## Migration Notes

No database migration is planned. The phase tests existing auth persistence and
may refactor test files and small internal helper seams only.

## References

- Related research: `context/changes/testing-auth-privacy-regression-floor/research.md`
- Test rollout source: `context/foundation/test-plan.md`
- Existing auth tests: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs`
- Auth service boundary: `LinuxGameCompat/Services/MagicLinkService.cs`
- Development sender boundary: `LinuxGameCompat/Services/LoggingAuthEmailSender.cs`
- Production sender boundary: `LinuxGameCompat/Services/SmtpAuthEmailSender.cs`
- Login UI normalization: `LinuxGameCompat/Components/Pages/Login.razor`
- Internal test access: `LinuxGameCompat/Properties/AssemblyInfo.cs`
- Historical auth plan: `context/archive/2026-05-31-passwordless-member-access/plan.md`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Extract Auth Test Support

#### Automated

- [x] 1.1 `dotnet build LinuxGameCompat.sln --no-restore` passes after test-support extraction — c459d7e
- [x] 1.2 `dotnet test LinuxGameCompat.sln --no-restore` passes with the existing 39 tests — c459d7e
- [x] 1.3 Existing auth tests still use real PostgreSQL-backed Identity service wiring — c459d7e

#### Manual

- [x] 1.4 Review confirms no production app behavior changed in this structural phase — c459d7e

### Phase 2: Auth Safety Regressions And Return URL Hardening

#### Automated

- [x] 2.1 `dotnet build LinuxGameCompat.sln --no-restore` passes after shared normalization changes — a48fc23
- [x] 2.2 `dotnet test LinuxGameCompat.sln --no-restore` passes with new auth safety tests — a48fc23
- [x] 2.3 Existing-vs-new valid email requests have equivalent accepted service outcomes — a48fc23
- [x] 2.4 Invalid, expired, consumed, and replayed consume attempts do not create or sign in unintended members — a48fc23
- [x] 2.5 Raw and encoded slash/backslash external-looking return URL cases normalize to `/` — a48fc23
- [x] 2.6 Ordinary local return URLs still round-trip through request and consume behavior — a48fc23

#### Manual

- [x] 2.7 Review confirms return URL hardening is shared by service and login UI normalization — a48fc23

### Phase 3: Privacy Boundary Regressions

#### Automated

- [x] 3.1 `dotnet build LinuxGameCompat.sln --no-restore` passes after sender composition seam changes
- [x] 3.2 `dotnet test LinuxGameCompat.sln --no-restore` passes with new privacy boundary tests
- [x] 3.3 Raw token is absent from saved `MagicLinkRequest` rows
- [x] 3.4 Raw token, `token=`, and full login links are absent from send-failure warning logs
- [x] 3.5 Failed sends leave no active magic-link request row
- [x] 3.6 Production email composition includes the intended login link and no extra auth material
- [x] 3.7 Development full-link logging is covered as an explicit local-only exception, not as a privacy failure

#### Manual

- [x] 3.8 Review confirms the privacy assertions match the accepted policy: normalized email is allowed in failed-send logs, Development full-link logging is an exception

### Phase 4: Cookbook And Handoff Gates

#### Automated

- [ ] 4.1 `dotnet test LinuxGameCompat.sln --no-restore` passes after cookbook updates
- [ ] 4.2 `context/foundation/test-plan.md` §6.1 documents auth/privacy regression test patterns
- [ ] 4.3 `context/foundation/test-plan.md` §6.6 records Phase 1 notes and 2026-06-03 AI-native guidance

#### Manual

- [ ] 4.4 Review confirms the cookbook matches the implemented tests and accepted scope boundaries
- [ ] 4.5 Review confirms `context/changes/testing-auth-privacy-regression-floor/change.md` remains `status: planned` until implementation begins
