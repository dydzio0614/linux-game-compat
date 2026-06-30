---
date: 2026-06-29T18:23:56+02:00
researcher: Codex
git_commit: 230db95e7c596c74f21480d706e07245d0db4509
branch: generate-evidence-claims-from-source-pages
repository: LinuxGameCompat
topic: "How should generate-evidence-claims-from-source-pages fit into the existing code?"
tags: [research, codebase, evidence-generation, source-pages, openai]
status: complete
last_updated: 2026-06-29
last_updated_by: Codex
last_updated_note: "Confirmed that both ProtonDB and Are We Anti-Cheat Yet adapters are in scope"
---

# Research: Generate Evidence Claims from Source Pages

**Date**: 2026-06-29T18:23:56+02:00  
**Researcher**: Codex  
**Git Commit**: 230db95e7c596c74f21480d706e07245d0db4509  
**Branch**: generate-evidence-claims-from-source-pages  
**Repository**: LinuxGameCompat

## Research Question

How should `generate-evidence-claims-from-source-pages` fit into the existing code?

## Summary

Implement S-09 as a separate, bounded `EvidenceGeneration` pipeline that runs before the existing summary generator. It should be a finite operator command, not a public request path and not additional logic inside `CompatibilitySummaryGenerator`.

The pipeline should select known `SourceReference` rows, fetch through source-specific allowlisted adapters for both ProtonDB and Are We Anti-Cheat Yet, extract source-native status deterministically, ask OpenAI only for bounded source-grounded claim text, and reconcile `EvidenceClaim` rows atomically. Existing read models and UI already consume those claims and their citation links, while the existing summary generator can synthesize them without changes.

The required new persistence concern is import ownership and freshness. The current schema has no content hash, fetch lifecycle, or generated-claim identity. For the MVP, make the importer the exclusive owner of claims attached to supported non-`Manual` source references, persist one import-state row per source reference, make unchanged normalized content a no-op, and mark the existing summary stale only when the persisted claim set changes. This is simpler and safer than mixing curated and generated claims on the same source reference without provenance.

## Detailed Findings

### Existing upstream and downstream boundaries

- `SourceReference` is already the canonical game/source/citation aggregate and owns its claims ([`LinuxGameCompat/Data/SourceReference.cs:3`](../../../LinuxGameCompat/Data/SourceReference.cs)). The source identity is uniquely constrained by source system and source-native game ID ([`LinuxGameCompat/Data/CompatibilityDbContext.cs:54`](../../../LinuxGameCompat/Data/CompatibilityDbContext.cs)).
- `EvidenceClaim` is the correct output entity: it stores the source-reference FK, claim type, native or compact value, human-readable assertion, and observation time ([`LinuxGameCompat/Data/EvidenceClaim.cs:11`](../../../LinuxGameCompat/Data/EvidenceClaim.cs)). The supported categories already match S-09: status, caveat, workaround, and note ([`LinuxGameCompat/Data/EvidenceClaimType.cs:6`](../../../LinuxGameCompat/Data/EvidenceClaimType.cs)).
- There is no source-page acquisition path today: no HTTP client registration, HTML parser, source adapter, or ingestion command. The current URLs are citation data only.
- The public read service already loads claims with their source references and maps them into the detail contract ([`LinuxGameCompat/Services/GameCompatibilityReadService.cs:79`](../../../LinuxGameCompat/Services/GameCompatibilityReadService.cs)). The detail page groups and renders the current claim types with the canonical source link ([`LinuxGameCompat/Components/Pages/GameDetail.razor:119`](../../../LinuxGameCompat/Components/Pages/GameDetail.razor)). No UI/read-model change is required for the first implementation.
- The summary generator already selects all persisted claims for visible games and carries their source URL into its evidence contract ([`LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:166`](../../../LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs)). S-09 should therefore stop at producing trustworthy persisted claims.

### Recommended application flow

Use a sibling command such as:

```text
generate-evidence-claims [--limit <n>] [--slug <slug>] [--force]
```

The application flow should be:

1. Select a bounded set of supported, non-`Manual` `SourceReference` rows, ordered by missing/oldest import attempt.
2. Resolve the source-specific adapter from `SourceSystemType`; reject any URL whose scheme, host, port, or path does not match that adapter's contract.
3. Fetch with strict timeout, redirect, response-size, and content-type limits. Redirect targets must be revalidated.
4. Parse only the adapter-defined authoritative page fragment. Produce bounded normalized text plus the exact source-native status value.
5. Normalize status with the existing deterministic mapping. Unknown native values should fail or be recorded as unmapped; they must not be guessed by the model ([`LinuxGameCompat/Services/SummaryGeneration/NativeStatusNormalizer.cs:7`](../../../LinuxGameCompat/Services/SummaryGeneration/NativeStatusNormalizer.cs)).
6. Send sanitized, bounded page text to a dedicated OpenAI claim-extraction provider with a strict JSON schema. The model may classify and word caveat/workaround/note claims, and may word the status claim, but it must not invent the authoritative status value.
7. Re-read/revalidate the import input, then transactionally reconcile the source reference's generated claim set and import state. Preserve last-known good claims on fetch, parse, or provider failure.
8. If the semantic claim set changed, set the game's existing summary to stale in the same transaction. Run `generate-summaries` separately afterward.

Two explicit commands are the better MVP operational contract. Automatic command composition can be added after refresh behavior is measured; it is not needed to establish correct data ownership.

### Service and folder placement

The feature will require at least three related implementation files, so repository rules place it in a feature folder, for example:

```text
LinuxGameCompat/Services/EvidenceGeneration/
  GenerateEvidenceClaimsCommand.cs
  EvidenceClaimGenerator.cs
  EvidenceGenerationOptions.cs
  SourcePageAdapter.cs
  ProtonDbPageAdapter.cs
  AreWeAntiCheatYetPageAdapter.cs
  EvidenceClaimPromptBuilder.cs
  EvidenceClaimProviderContracts.cs
  OpenAiEvidenceClaimProvider.cs
```

Implement both supported sources behind a small common adapter interface, with a separate deterministic parsing contract and fixture suite for each. A generic HTML extraction framework is not justified: ProtonDB and Are We Anti-Cheat Yet have different identities and page structures and should not be forced through one speculative parser ([`LinuxGameCompat/Data/Seed/CompatibilitySeedData.cs:84`](../../../LinuxGameCompat/Data/Seed/CompatibilitySeedData.cs)). Shared code should be limited to transport safeguards, bounded-content handling, and the adapter result contract.

Register this feature beside summary generation in `Program.cs`, with its own `EvidenceGeneration` configuration section and command-only credential validation. Reuse the current finite-process pattern—configuration validation, scoped orchestrator, Ctrl+C cancellation, aggregate result, and explicit exit code—from [`LinuxGameCompat/Program.cs:66`](../../../LinuxGameCompat/Program.cs).

### OpenAI integration: reuse patterns, not summary contracts

- Do not reuse or extend `ICompatibilitySummaryProvider`. Its request, output, instructions, and schema are specifically `{status, summary}` ([`LinuxGameCompat/Services/SummaryGeneration/ProviderContracts.cs:7`](../../../LinuxGameCompat/Services/SummaryGeneration/ProviderContracts.cs)). Claim extraction has multiple typed outputs and different validation rules.
- Reuse the proven behavior of the Responses API adapter: strict JSON Schema, `StoredOutputEnabled = false`, bounded output tokens, SDK timeout/retries, completion checks, local parsing, and failure classification ([`LinuxGameCompat/Services/SummaryGeneration/OpenAiCompatibilitySummaryProvider.cs:20`](../../../LinuxGameCompat/Services/SummaryGeneration/OpenAiCompatibilitySummaryProvider.cs)). Avoid building a generic generation framework for two workflows.
- Add explicit hostile-input instructions: page content is data, instructions found in it are not commands. This complements, but does not replace, deterministic HTML selection and local output validation.
- Validate output count, enum values, duplicates, blank values, and the database limits of 120 characters for `ClaimValue` and 2,000 for `ClaimText` ([`LinuxGameCompat/Data/CompatibilityDbContext.cs:63`](../../../LinuxGameCompat/Data/CompatibilityDbContext.cs)).

### Persistence, freshness, and idempotency

The current claim table permits duplicates and contains no import provenance or natural key ([`LinuxGameCompat/Data/CompatibilityDbContext.cs:63`](../../../LinuxGameCompat/Data/CompatibilityDbContext.cs)). `SourceReference.MetadataJson` exists, but opaque JSON should not own operational eligibility, failures, and freshness.

Add a small one-to-one import-state entity keyed by `SourceReferenceId`, carrying at minimum:

- normalized content hash;
- extraction contract/version;
- last successful fetch/import time;
- last attempted time;
- optional ETag/Last-Modified values;
- bounded failure code/message.

For the MVP, define supported external source references as generator-owned and `Manual` references as curator-owned. This permits atomic replacement without adding speculative per-claim provenance. If generated and curated claims must later coexist under one external source reference, add explicit claim origin and a stable generated key before allowing that workflow.

An unchanged normalized content hash must perform no claim mutation. This matters because summary canonicalization includes claim IDs and observation timestamps ([`LinuxGameCompat/Services/SummaryGeneration/EvidencePromptBuilder.cs:60`](../../../LinuxGameCompat/Services/SummaryGeneration/EvidencePromptBuilder.cs)); delete/reinsert on every run would make every summary stale even when source meaning was unchanged.

When content meaning changes, atomic replacement is acceptable and should deliberately change the summary evidence hash. Mark the summary stale immediately rather than waiting for a later summary run to discover the mismatch. The summary generator already rejects output when evidence changes during its provider call through a recheck and short serializable transaction ([`LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:72`](../../../LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs)); the importer must use compatible short transactions and never hold a transaction across HTTP or OpenAI calls.

### Security and operational boundaries

- Stored source URLs currently receive only absolute HTTP(S) validation ([`LinuxGameCompat/Services/CompatibilityDataValidator.cs:60`](../../../LinuxGameCompat/Services/CompatibilityDataValidator.cs)). That is insufficient for server-side fetching. Source adapters must allowlist exact public hosts and path shapes and reject credentials, unexpected ports, IP literals, local/private destinations, and cross-host redirects.
- Bound downloaded bytes before parsing, accept only intended content types, use short timeouts, and process sequentially for the MVP.
- Use a claim-generation advisory-lock key distinct from the summary generator's key. Avoid duplicate paid work within ingestion, while relying on the summary generator's evidence recheck to handle concurrent claim commits.
- Never fetch on anonymous requests. The PRD requires useful detail output within ten seconds and source-backed uncertainty, which favors precomputed stored claims ([`context/foundation/prd.md:98`](../../foundation/prd.md)).

### Test seams and verification

- Parser tests should use checked-in representative HTML fixtures; tests must not depend on live source pages.
- Unit tests should cover URL/redirect allowlisting, byte/content-type limits, deterministic native-status extraction, hostile/bad HTML, prompt budgeting, strict provider output parsing, length/count/deduplication, and command parsing.
- PostgreSQL integration tests should cover bounded eligibility, hidden/manual exclusions, advisory-lock contention, unchanged no-op with stable IDs/timestamps, changed atomic replacement, duplicate prevention, stale-summary transition, last-good preservation on fetch/model failure, and concurrent summary generation.
- Existing read-contract tests should continue proving that claims retain verifiable links and no-evidence games remain uncertain. The test strategy already names provenance, freshness, idempotency, throttling, duplicate extraction, source failures, and poisoned external data as S-09 risks ([`context/foundation/test-plan.md:64`](../../foundation/test-plan.md)).

## Code References

- `LinuxGameCompat/Data/SourceReference.cs:3-41` - Canonical external identity and citation owner.
- `LinuxGameCompat/Data/EvidenceClaim.cs:11-39` - Existing claim persistence target.
- `LinuxGameCompat/Data/CompatibilityDbContext.cs:54-69` - Current source/claim constraints and persistence gap.
- `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:22-178` - Finite generation, locking, recheck, stale lifecycle, and downstream claim consumption.
- `LinuxGameCompat/Services/SummaryGeneration/NativeStatusNormalizer.cs:7-47` - Deterministic source-native mapping and pessimistic reduction.
- `LinuxGameCompat/Services/SummaryGeneration/OpenAiCompatibilitySummaryProvider.cs:20-57` - Existing bounded Responses API pattern.
- `LinuxGameCompat/Services/GameCompatibilityReadService.cs:79-121` - Existing public mapping of claims and citations.
- `LinuxGameCompat/Components/Pages/GameDetail.razor:119-207` - Existing claim/source rendering.
- `LinuxGameCompat/Program.cs:48-105` - Configuration, DI, and finite command dispatch pattern.
- `context/foundation/roadmap.md:200-212` - S-09 outcome, unknowns, and trust boundary.

## Architecture Insights

S-09 is an upstream materialization stage:

```text
known SourceReference
  -> source-specific bounded fetch/parser
  -> deterministic native status + sanitized source facts
  -> bounded OpenAI claim wording/classification
  -> atomic EvidenceClaim reconciliation + import state
  -> stale GameCompatibilitySummary
  -> existing generate-summaries command
  -> unchanged public read/UI path
```

This preserves the domain model's established trust hierarchy: raw source-linked claims are authoritative inputs; deterministic native status wins when recognized; AI summaries are derived output. It also isolates two distinct paid-generation contracts without introducing a generic framework.

## Historical Context (from prior changes)

- [`context/archive/2026-05-27-minimal-evidence-baseline/plan.md`](../../archive/2026-05-27-minimal-evidence-baseline/plan.md) established claim-granular evidence and normalized citation ownership through `SourceReference`.
- [`context/archive/2026-05-27-minimal-evidence-baseline/implementation-notes.md`](../../archive/2026-05-27-minimal-evidence-baseline/implementation-notes.md) reserved generated summaries as consumers of source-backed claims rather than replacements for them.
- [`context/archive/2026-06-14-generated-compatibility-synthesis/plan.md`](../../archive/2026-06-14-generated-compatibility-synthesis/plan.md) explicitly excluded scraping/ingestion, established complete-evidence hashing, and made deterministic native status authoritative over AI output.
- [`context/archive/2026-06-14-generated-compatibility-synthesis/reviews/impl-review.md`](../../archive/2026-06-14-generated-compatibility-synthesis/reviews/impl-review.md) records the race fix that added the current recheck/short-transaction locking behavior.
- [`context/archive/2026-06-26-simplify-summary-generation-code/frame.md`](../../archive/2026-06-26-simplify-summary-generation-code/frame.md) requires preserving provider isolation, hashing, lifecycle, locking, deterministic status precedence, and output validation while avoiding unnecessary type surface.

## Related Research

- [`context/archive/2026-06-14-generated-compatibility-synthesis/research.md`](../../archive/2026-06-14-generated-compatibility-synthesis/research.md) - Finite Railway command pattern and stored-summary boundary.
- [`context/foundation/test-plan.md`](../../foundation/test-plan.md) - Existing S-09 risk and acceptance coverage.

## Open Questions

- Which exact page fragment is authoritative for each ProtonDB and Are We Anti-Cheat Yet adapter?
- Does normalized content hashing exclude volatile page elements, and which semantic changes force extraction? This belongs in the chosen adapter's contract.
- Should a source-native status claim's text be generated by OpenAI while its value remains deterministic, or should the whole status claim be templated?
- How old may last-known good claims become before the UI must disclose evidence staleness? Existing UI exposes summary staleness but not source-fetch freshness.
- Should public `Game.CompatibilityStatus` remain last-known until `generate-summaries` succeeds? This matches current behavior and is recommended unless the product contract is intentionally changed.

## Follow-up Research 2026-06-29T18:26:01+02:00

The user confirmed that both ProtonDB and Are We Anti-Cheat Yet adapters are required in S-09. This resolves the source-scope question but does not justify a shared page parser. The implementation should provide two source-specific adapters behind one narrow acquisition contract, share only HTTP safety and bounded-content infrastructure, and verify each adapter against its own checked-in fixtures. Both adapter outputs feed the same claim-generation and reconciliation pipeline.
