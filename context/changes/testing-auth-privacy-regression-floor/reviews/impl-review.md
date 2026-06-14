<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Auth And Privacy Regression Floor

- **Plan**: `context/changes/testing-auth-privacy-regression-floor/plan.md`
- **Scope**: Phases 1-4 of 4
- **Date**: 2026-06-03
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Verification

- `dotnet build LinuxGameCompat.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LinuxGameCompat.sln --no-restore` passed with 64 passed, 0 failed, 0 skipped.
- Manual progress evidence matched the completed `## Progress` checklist.
- The explicit residual scoped gap remains no TestServer/browser proof of auth-cookie issuance or final HTTP `Location` behavior; that was deliberately deferred by the plan.

## Findings

### F1 - Failure-log test misses exception-rendered token leaks

- **Severity**: WARNING
- **Impact**: MEDIUM - real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: `LinuxGameCompat/Services/MagicLinkService.cs:72`
- **Detail**: The send-failure path logged the caught exception after the raw login link had already crossed into `IAuthEmailSender`. The regression test checked only the formatted log message, while real providers commonly render exception text separately. A sender exception that embedded the message body, login link, or token could have passed tests while leaking the raw token through rendered exception output.
- **Fix**: Extend the privacy boundary test so captured log entries include rendered exception text, make the throwing sender include the login link/token in its exception, then sanitize production failure logging until raw token, `token=`, and full links are absent from both message and exception output.
  - Strength: Tests the same log surface production operators actually receive and keeps the privacy guarantee end to end.
  - Tradeoff: Reduces exception detail in auth email send warnings.
  - Confidence: HIGH - Microsoft logging APIs pass exception separately, and the previous test logger only stored `formatter(...)`.
  - Blind spot: Did not inspect every possible production SMTP exception shape, but an accidental sender exception with link content was enough to expose the gap.
- **Decision**: FIXED - sanitized `MagicLinkService` send-failure logging to omit the exception object, updated the throwing test sender to include the generated login link in the thrown exception, and asserted privacy against combined formatted message plus rendered exception text.
