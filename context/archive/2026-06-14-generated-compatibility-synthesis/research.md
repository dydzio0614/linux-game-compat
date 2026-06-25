---
date: 2026-06-18T23:45:09+02:00
researcher: Codex
git_commit: 5c2afa06586fc8f74fb4a709e72aab8d36d889c6
branch: generated-compat-synthesis
repository: LinuxGameCompat
topic: "Will a command-line switch for a special application mode be a viable way to add data generation when production is deployed on Railway?"
tags: [research, codebase, railway, compatibility-synthesis, cli]
status: complete
last_updated: 2026-06-18
last_updated_by: Codex
---

# Research: CLI Data Generation on Railway

**Date**: 2026-06-18T23:45:09+02:00  
**Researcher**: Codex  
**Git Commit**: 5c2afa06586fc8f74fb4a709e72aab8d36d889c6  
**Branch**: generated-compat-synthesis  
**Repository**: LinuxGameCompat

## Research Question

Will a command-line switch for a special application mode be a viable way of adding compatibility-summary data generation if production is deployed on Railway?

## Summary

Yes. A finite `generate-summaries` application mode is a good boundary for offline generation, provided it runs as a separate Railway scheduled service using the same image and PostgreSQL database. It must not replace or run inside the production web service lifecycle.

Railway cron jobs execute a service's Start Command on a schedule and expect the process to terminate. Railway also allows a custom Start Command to override a Docker image's `ENTRYPOINT`. These capabilities fit a command such as `dotnet LinuxGameCompat.dll generate-summaries --limit 10` without requiring another codebase or image.

The current Docker entrypoint is web-specific and does not reliably forward appended arguments, so the generation service requires an explicit Railway Start Command override. Public web requests should only read stored summaries; generation should remain bounded, idempotent, and protected against concurrent execution.

This research deliberately treats the current branch as an independent implementation attempt. Findings from any other local implementation branch are excluded.

## Detailed Findings

### Application entry point

- The executable currently always creates a `WebApplication`, configures the HTTP pipeline, and calls `app.Run()` (`LinuxGameCompat/Program.cs:10-54`, `LinuxGameCompat/Program.cs:112`).
- CLI dispatch can occur before normal web startup. The command path can build the same dependency-injection container, create a scope, run generation, dispose resources, and return an explicit exit code without starting Kestrel.
- Generation orchestration should live behind a scoped service. `Program.cs` should own only mode detection, argument validation, cancellation wiring, result reporting, and exit codes.
- Normal web startup must not require provider credentials. Provider configuration should be validated only when generation mode is selected.

### Railway execution model

- The current image always starts the web application through a shell-form command (`Dockerfile:14-22`). Appending arguments to that container command will not reliably select a generation mode.
- Railway permits a service-specific Start Command that overrides the Docker `ENTRYPOINT`: [Railway build and start commands](https://docs.railway.com/reference/build-and-start-commands).
- Railway cron services execute their Start Command on a UTC schedule, must terminate after completing work, and skip a new execution while the previous one remains active: [Railway cron jobs](https://docs.railway.com/reference/cron-jobs).
- Use a separate Railway service sourced from the same repository and Dockerfile. Its Start Command should be the complete generation command, while the existing `linux-game-compat` service retains its HTTP command.
- Do not use a pre-deploy command. Railway runs it as part of deployment, does not retry failures, and blocks deployment when it fails: [Railway pre-deploy commands](https://docs.railway.com/guides/pre-deploy-command).
- `railway run` executes locally with Railway variables rather than providing production compute, so it is not the production job runner: [Railway CLI run](https://docs.railway.com/cli/run).
- Running the command through SSH in the live web container is possible but should be break-glass only because it shares resources and failure scope with public traffic: [Railway CLI SSH](https://docs.railway.com/cli/ssh).

### Free-plan viability

- Railway Free currently provides $1 of monthly usage credit and limits each service to one replica, 0.5 GB RAM, and 1 vCPU: [Railway plans](https://docs.railway.com/reference/pricing/plans).
- A scheduled service is technically available and consumes compute only while executing, making bounded batches the lowest-overhead production option.
- The repository's Railway project already contains an online web service and PostgreSQL. A third scheduled service adds limited runtime usage, but the combined web, database, generation, network, and provider costs are not guaranteed to fit the $1 credit.
- A persistent queue worker is a poor Free-plan fit. It adds continuously running compute and is unnecessary for operator-controlled or scheduled MVP batches.

### Existing persistence and read path

- Production database configuration already supports Railway's `DATABASE_URL` (`LinuxGameCompat/Data/CompatibilityDbContextOptions.cs:7-24`). The generation service can reuse the same PostgreSQL database.
- `GameCompatibilitySummary` already stores lifecycle state, status, text, provider/model provenance, evidence version/hash, generation time, staleness, and error metadata (`LinuxGameCompat/Data/GameCompatibilitySummary.cs:7-51`).
- The database enforces one summary row per game (`LinuxGameCompat/Data/CompatibilityDbContext.cs:71-83`). This prevents duplicate rows but does not prevent duplicate provider calls or lost updates.
- The read service already loads and maps optional summaries (`LinuxGameCompat/Services/GameCompatibilityReadService.cs:70-88`, `LinuxGameCompat/Services/GameCompatibilityReadService.cs:144-163`).
- The detail page currently renders any non-empty summary text without considering lifecycle state or staleness (`LinuxGameCompat/Components/Pages/GameDetail.razor:78-84`). Generated-summary UX must distinguish current, stale, failed, and unavailable output.

### Generation safety and data flow

- Eligibility should require a visible game with at least one curated evidence claim. No-evidence games are valid and must remain explicitly uncertain.
- Compute a deterministic hash from the complete, ordered evidence set. Prompt-size caps must be applied only after freshness and status calculations so omitted prompt claims cannot leave a summary falsely current.
- Make each execution bounded with a hard game limit, claim/input cap, output-token cap, request timeout, retry cap, and provider concurrency of one for the MVP.
- Acquire a PostgreSQL advisory lock or equivalent durable lease before selecting work. The unique summary index alone cannot stop concurrent processes from paying for the same provider request.
- Do not hold an EF transaction or row lock across an external provider call. Recheck the evidence hash before committing the result and discard output when evidence changed during generation.
- Provider output must be structured and validated. Status values should use exact normalized mappings; substring parsing can incorrectly interpret negated text such as `not playable`.
- Sanitize and truncate persisted error codes/messages to the mapped column limits (`LinuxGameCompat/Data/CompatibilityDbContext.cs:80-81`). Propagate requested cancellation instead of recording it as a provider failure.
- A failed refresh should preserve the last successful summary text and provenance, mark it stale, and record operator-facing failure metadata. Public UI must not expose provider errors.

### Product alignment

- S-04 requires source-grounded summaries, retry/failure behavior, cost controls, telemetry, and traceability (`context/foundation/roadmap.md:127-139`).
- The PRD requires source links for compatibility claims and a useful detail result within ten seconds (`context/foundation/prd.md:98-107`). Pre-generated summaries satisfy the latency constraint without exposing provider calls to anonymous traffic.
- Raw evidence and links should remain visible beneath generated text. A generated statement must not become a replacement for the source-backed evidence model.

## Code References

- `LinuxGameCompat/Program.cs:10-54` - Existing dependency registrations and unconditional web-host construction.
- `LinuxGameCompat/Program.cs:112` - Unconditional web application run boundary.
- `Dockerfile:14-22` - Runtime image and web-specific shell entrypoint.
- `LinuxGameCompat/Data/CompatibilityDbContextOptions.cs:7-24` - Railway `DATABASE_URL` support.
- `LinuxGameCompat/Data/GameCompatibilitySummary.cs:7-51` - Existing summary cache and lifecycle fields.
- `LinuxGameCompat/Data/CompatibilityDbContext.cs:71-83` - Summary mappings and unique game constraint.
- `LinuxGameCompat/Services/GameCompatibilityReadService.cs:70-88` - Detail query includes evidence and summary.
- `LinuxGameCompat/Components/Pages/GameDetail.razor:78-145` - Current summary and source-evidence rendering.
- `context/foundation/roadmap.md:127-139` - S-04 requirements and risks.
- `context/foundation/prd.md:98-107` - Traceability and response-time requirements.

## Architecture Insights

Use one deployable artifact with two process modes and two Railway service definitions:

1. Web service: long-running HTTP process; reads cached summaries.
2. Cron service: finite CLI process; generates bounded batches and exits.

This keeps provider cost away from public traffic, avoids duplicating domain/configuration code, and defers queue infrastructure until generation becomes demand-driven. PostgreSQL remains the durable coordination and output boundary; container storage is irrelevant.

## Historical Context

- `context/archive/2026-05-27-minimal-evidence-baseline/implementation-notes.md` reserved the summary schema for later generated output and kept production migrations explicit.
- `context/foundation/infrastructure.md:63-77` anticipated separate service boundaries for background generation while warning against premature worker/queue complexity.
- Other implementation branches are intentionally excluded at the user's direction; this research is grounded in the current branch and accepted foundation/archive documents only.

## Related Research

- `context/foundation/infrastructure.md` - Railway selection, operational constraints, and cost risks.
- `context/archive/2026-05-27-minimal-evidence-baseline/plan.md` - Original summary-ready schema and explicit migration policy.

## Open Questions

- Select the provider/model and define hard per-run provider-cost limits before implementation planning.
- Choose the initial UTC cron frequency and batch size after measuring one representative generation run.
- Decide whether a supplied `--slug` always forces regeneration or requires a separate `--force` flag.
- Confirm the current Railway account tier and usage baseline before assuming the combined deployment remains within Free credit.
