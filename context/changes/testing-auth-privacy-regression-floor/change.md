---
change_id: testing-auth-privacy-regression-floor
title: Auth and privacy regression floor
status: implementing
created: 2026-06-03
updated: 2026-06-03
archived_at: null
---

## Notes

Open a change folder for rollout Phase 1 of context/foundation/test-plan.md: "Auth and privacy regression floor".
Risks covered: #1, #2. Test types planned: integration + service.
Risk response intent:
- #1: invalid, expired, consumed, and replayed links must not sign in; request responses stay generic; unsafe return URLs must not redirect externally.
- #2: stored, logged, sent, and failed-email artifacts must not expose raw tokens or unnecessary user-email/account information.
After creating the folder, follow the downstream continuation rule.
