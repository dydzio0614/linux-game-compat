# Generated Compatibility Synthesis — Plan Brief

> Full plan: `context/changes/generated-compatibility-synthesis/plan.md`
> Research: `context/changes/generated-compatibility-synthesis/research.md`

## What & Why

Add bounded, offline compatibility-summary generation from curated source evidence. Users receive concise generated prose while deterministic source-native status rules remain authoritative and raw source links remain available for verification.

## Starting Point

The database and read path already support one optional summary per game, but no generator exists and the UI ignores lifecycle/staleness. The app currently has only a long-running web mode; Railway runs the web service and PostgreSQL.

## Desired End State

An operator can run a finite `generate-summaries` command that safely writes current summaries and normalized statuses. Public details distinguish current, stale, failed, fallback, and disagreement states without exposing provider errors or replacing source evidence.

## Key Decisions Made

| Decision | Choice | Why | Source |
|---|---|---|---|
| Process boundary | Same artifact, separate finite CLI mode | Keeps provider cost and failures out of public requests | Research |
| Provider | OpenAI Responses API, `gpt-5.4-mini` | Low-cost structured generation; model remains configurable | Plan |
| Public status | Deterministic pessimistic source reduction | Prevents optimistic AI output from overriding evidence | Plan |
| ProtonDB Gold | `Playable` | Gold is accepted as practically playable despite required tweaks | Plan |
| AWA Planned | `Unsupported` | Product describes current usability, not future prognosis | Plan |
| AI status | Fallback only; advisory on disagreement | Preserves usefulness when native values do not map | Plan |
| Cost boundary | 10 games, 12 claims, 2,500 input/500 output tokens | Bounds Railway and provider usage | Plan |
| Retry policy | Official SDK only; 30 seconds per attempt and two retries | Prevents nested retry multiplication while handling ordinary provider instability | Plan / Review |
| Targeting | `--slug` plus explicit `--force` | Prevents accidental paid regeneration | Plan |
| Failure UX | Preserve prose with trust labels | Keeps useful context without hiding staleness | Plan |
| Coordination | PostgreSQL advisory lock, lock contention exits 0 | Prevents duplicate paid calls safely | Research / Plan |
| Initial operation | Manual Railway service, no cron | Measure quality, runtime, and cost before scheduling | Plan |
| Telemetry | Latest summary metadata plus aggregate command result | Avoids a new ledger while supporting initial measurement | Plan |

## Scope

**In scope:**

- Native ProtonDB and Are We Anti-Cheat Yet status parsing.
- Deterministic status reduction and AI fallback/advisory behavior.
- Evidence hashing, prompt/token limits, OpenAI integration, retries, and validation.
- Safe summary persistence, advisory locking, CLI dispatch, and lifecycle-aware UI.
- One manually triggered Railway service and representative production run.

**Out of scope:**

- Evidence scraping or refresh, public on-demand generation, queues, persistent workers, cron scheduling, run ledgers, personalized analysis, or sentence-level citations.

## Architecture / Approach

Curated evidence is canonicalized and fully hashed. Native status parsers produce a pessimistic deterministic status, while a capped subset goes to OpenAI for prose and an advisory status. A locked finite process rechecks the hash before committing to PostgreSQL; the web service only reads stored results and renders trust state alongside raw evidence.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Native normalization and contracts | Status parsers, hashing, prompt limits, provider boundary | Incorrect native mapping or prompt truncation |
| 2. Safe orchestration and CLI | Locking, lifecycle persistence, retries, command mode | Duplicate spend or stale writes |
| 3. Trust-aware UI | Honest lifecycle, fallback, and disagreement rendering | Generated prose appears more authoritative than evidence |
| 4. Manual Railway rollout | One finite production service and measured run | Provider/Railway cost or runtime exceeds MVP limits |

**Prerequisites:** Existing PostgreSQL schema, curated evidence, Railway access, and an OpenAI API key with a strict spending limit.

**Estimated effort:** Approximately 4 implementation sessions across 4 gated phases.

## Open Risks & Assumptions

- `gpt-5.4-mini` availability and the current official SDK/API contract must be re-verified before implementation; no silent model substitution is allowed.
- Provider requests use no reasoning, low verbosity, strict structured output, and a 500-token output cap; incomplete output is rejected.
- Upstream native vocabularies can change, so unrecognized values intentionally yield no deterministic signal until mappings are updated.
- Railway remains manual and unscheduled until the representative run is reviewed.
- Existing uncommitted `change.md` and `research.md` work belongs to the user and must be preserved.

## Success Criteria (Summary)

- Native statuses reduce deterministically, including ProtonDB Gold as `Playable` and AWA Planned as `Unsupported`.
- Generated prose is bounded, freshness-safe, retry-safe, and never generated twice concurrently.
- Public pages disclose stale, failed, fallback, and disagreement states while preserving source-linked evidence.
