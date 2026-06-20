# Generated Compatibility Synthesis — Implementation Notes

## Operator Handoff

- Apply `20260619192229_AddSummaryAttemptMetadata` explicitly before the first production generation run.
- Use a separate Railway service built from the existing image with Start Command `dotnet LinuxGameCompat.dll generate-summaries --limit 10`.
- Share `DATABASE_URL`, scope `OPENAI_API_KEY` to the generation service, and set restart policy to `Never`.
- Do not configure a cron schedule in this change.
- Raw source-linked evidence remains authoritative; generated prose and AI status are advisory where the UI says so.

## Deferred Production Rollout

Live Railway service creation, migration, provider spend, representative generation, idempotency rerun, and resource measurement were removed from this change's scope on 2026-06-20. They are tracked as roadmap slice `S-05: production-summary-generation-rollout`.

The implementation is ready for that follow-up because the same published artifact supports a finite `generate-summaries` mode, accepts Railway's shared `DATABASE_URL`, validates `OPENAI_API_KEY` only in generation mode, uses a PostgreSQL advisory lock, emits bounded aggregate usage, and leaves default web startup unchanged.
