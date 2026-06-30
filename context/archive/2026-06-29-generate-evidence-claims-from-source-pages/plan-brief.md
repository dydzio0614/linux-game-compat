# Generate Evidence Claims from Source Pages — Plan Brief

> Full plan: `context/changes/generate-evidence-claims-from-source-pages/plan.md`  
> Research: `context/changes/generate-evidence-claims-from-source-pages/research.md`

## What & Why

Implement S-09 so compatibility summaries are backed by claims generated from current ProtonDB and Are We Anti-Cheat Yet source data. Operators get one bounded command that refreshes evidence and summaries together, while users retain source links and last-known-good output when external systems fail.

## Starting Point

The data model, detail page, and summary generator already consume source-linked `EvidenceClaim` rows. The missing layer is safe source acquisition, import ownership/freshness, grounded claim generation, and orchestration that keeps claims and summaries current through one command.

## Desired End State

`refresh-compatibility [--limit] [--slug] [--force]` refreshes every supported reference for each selected visible game, reconciles changed claims, and then refreshes the summary. Manual-only games remain summary-eligible through the same command without source acquisition. An unchanged normal run fetches supported-source data but performs no OpenAI work or claim mutation. Source failures preserve existing claims and prose while marking the summary stale; unchanged recovery restores a matching summary without OpenAI work.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Source acquisition | First-party backing JSON | Avoids browser infrastructure while retaining deterministic source data | Plan |
| Supported sources | ProtonDB and AWA in the first slice | Both are required for S-09 | Research |
| Claim scope | Bounded authoritative facts | Limits hostile input, noise, and token cost | Plan |
| Status claim | Deterministic local template | The model cannot alter source-native status | Plan |
| Claim ownership | Importer owns supported external references | Avoids speculative per-claim provenance | Research / Plan |
| Failure behavior | Preserve last-known-good claims | Temporary external failure must not erase public evidence | Plan |
| Operator workflow | Replace summary-only command with one refresh command | Enforces evidence and summary refresh through one path | Plan |
| Manual-only summaries | Preserve them in the unified command without acquisition | Removing the old command must not drop an existing summary capability | Plan review |
| No-op behavior | Skip both models when current | Makes freshness checks safe and inexpensive | Plan |
| Partial success | Commit fresh claims before summary synthesis | Trustworthy evidence is not discarded if summary generation fails | Plan |

## Scope

**In scope:**

- Import-state schema and migration.
- Safe, fixture-tested ProtonDB and AWA adapters.
- Deterministic status claims and strict OpenAI non-status claims.
- Atomic per-game claim reconciliation and summary staleness.
- Unified finite command, fake-provider mode, metrics, tests, and operator documentation.

**Out of scope:**

- Headless browsers, individual ProtonDB report scraping, extra sources, scheduling, or fetch-on-read.
- Public refresh APIs, new UI, or source-freshness presentation.
- Mixed manual/generated ownership on one external source reference.

## Architecture / Approach

```text
selected visible game
  -> fetch + normalize every supported reference, or bypass acquisition for manual-only evidence
  -> semantic hash / no-op check
  -> deterministic status + bounded OpenAI non-status claims
  -> atomic claim reconciliation + import state
  -> per-game summary generation when eligible
  -> existing read service and detail page
```

Source URLs are constructed from validated identifiers, not fetched from arbitrary stored URLs. Network and model work happen outside database transactions; short serializable transactions protect claim and summary writes.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Import state and adapters | Migration, bounded transport, deterministic source contracts | Undocumented ProtonDB response changes |
| 2. Claim generation and reconciliation | Grounded claims, no-op hashing, ownership, atomic persistence | Model output or partial-source failure |
| 3. Unified refresh | Combined command, summary reuse, end-to-end verification | Preserving summary race protections during refactor |

**Prerequisites:** Reviewed source fixtures, Docker/PostgreSQL for integration tests, and `OPENAI_API_KEY` only for an approved live smoke run.  
**Estimated effort:** Approximately 3–5 implementation sessions across three reviewable phases.

## Open Risks & Assumptions

- ProtonDB's same-origin summary JSON is undocumented; contract versioning, fixtures, and a live pre-import check contain this risk.
- AWA's global canonical dataset must remain below the configured 8 MiB decompressed cap or the cap must be deliberately reviewed.
- Existing claims on supported references are replaced after the first successful import; back up persistent data before live rollout.
- Source failures block all claim changes for that game and mark retained summary prose stale; unchanged recovery clears that staleness without model work. A summary failure after claim commit intentionally leaves fresh claims and stale prose.

## Success Criteria (Summary)

- A bounded command refreshes both sources and produces source-linked claims plus a current summary.
- Immediate unchanged reruns preserve claim IDs/timestamps and make no OpenAI calls.
- Fetch, parsing, extraction, and summary failures retain last-known-good public data with bounded operator diagnostics.
