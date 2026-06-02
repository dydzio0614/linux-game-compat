# Passwordless Member Access Review Follow-Ups

## F4 - Magic-link request throttling remains a launch risk

- **Source**: `context/changes/passwordless-member-access/reviews/impl-review.md`
- **Status**: Pending before public or higher-volume launch.
- **Recommendation**: Add per-normalized-email and preferably per-IP throttling for `POST /auth/magic-link/request`, using `MagicLinkRequest.CreatedAt` and generic responses.
- **Reason**: The endpoint can generate, store, and send magic links without current abuse controls. This was an accepted MVP deferral, not implementation drift.
