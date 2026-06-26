# Frame Brief: Simplify Summary Generation Code

> Framing step before /10x-plan. This document captures what is *actually*
> at issue, separated from what was initially assumed.

## Reported Observation

S-04 summary generation code is perceived as overengineered, with too many datatypes or records, excessive validation checks, possible hardcoded config caps, and awkward class design. The code should be easier to understand for the MVP, tests should not drive unnecessary design complexity, and config files should remain the source of truth for default parameters.

## Initial Framing (preserved)

- **User's stated cause or approach**: The summary generation implementation likely has unnecessary type proliferation, duplicated/over-defensive validation, and config ownership drift from prior implementation.
- **User's proposed direction**: Simplify summary generation code so it is easier to understand and less oriented around hypothetical extension.
- **Pre-dispatch narrowing**: Not separated yet; treated as a cluster until code evidence separated type surface, config ownership, workflow safeguards, and test pressure.

## Dimension Map

The observation could originate at any of these dimensions:

1. **Configuration ownership** - defaults live in `appsettings.json`, but validation and tests may repeat fixed caps or model restrictions.
2. **Contract/type boundaries** - small records and interfaces split one finite command into many concepts, making the flow harder to trace. <- strongest user signal
3. **Operational workflow complexity** - advisory locks, stale detection, hash rechecks, and failure preservation may look complex but protect production behavior.
4. **Test pressure** - tests may preserve old plan wording or internal design shape instead of only guarding useful behavior.

## Hypothesis Investigation

| Hypothesis | Evidence | Verdict |
| --- | --- | --- |
| Configuration ownership is the root problem | Defaults and limits are in `LinuxGameCompat/appsettings.json:9`; `GenerationOptions` still has code defaults and validation at `LinuxGameCompat/Services/SummaryGeneration/GenerationOptions.cs:7` and `:17`. Prior commits `1c3cc87` and `454d9d2` already moved some validation to settings and removed duplicate CLI range validation. | WEAK |
| Contract/type boundaries are the root problem | `GenerateSummariesCommandOptions` is parsed at `LinuxGameCompat/Services/SummaryGeneration/GenerateSummariesCommand.cs:3` then immediately converted to `SummaryGenerationRunOptions` in `LinuxGameCompat/Program.cs:93`. `Candidate.Evidence` is set at `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:56` but not read, and the private record still carries it at `:186`. `ICompatibilitySummaryGenerator` has one implementation and is only resolved by `Program.cs:53`. User narrowing selected type surface as the strongest pain. | STRONG |
| Operational workflow complexity is the root problem | Advisory lock, stale detection, evidence hash recheck, failure preservation, cancellation, and ordering are covered by integration tests in `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs:91`, `:115`, `:142`, `:170`, and `:216`. S-04 review history explicitly fixed evidence race and provider failure behavior in `context/archive/2026-06-14-generated-compatibility-synthesis/reviews/impl-review.md:40` and `:48`. | NONE |
| Tests are the root problem | Some tests lock internal caps and shape, such as `LinuxGameCompat.Tests/SummaryGenerationContractTests.cs:78` and `:160`, and `LinuxGameCompat.Tests/GenerateSummariesCommandTests.cs:28`. Most generator tests, however, cover real safety behavior rather than arbitrary design. | WEAK |

## Narrowing Signals

- User selected **Type surface** as the strongest observable pain.
- Prior simplification commits already addressed some duplicated validation, reducing config ownership from primary root to secondary cleanup.
- Explorer review found a few likely collapsible boundaries, while provider contracts and evidence hashing remain behavior-bearing.

## Cross-System Convention

For a finite MVP command, a narrow provider boundary and durable run result are reasonable, but adjacent DTO handoffs and unused private record fields are unnecessary friction. External API output validation and persistence/concurrency safeguards should remain explicit because they defend paid provider calls, source-backed freshness, and public status correctness.

## Reframed Problem Statement

> **The actual problem to plan around is**: S-04 summary generation is hard to read because one finite MVP command is spread across too many small boundary types and seams, while some remaining config/default validation duplication adds secondary noise.

The initial framing was directionally right, but the frame should not treat the whole generator workflow as overengineering. Planning should focus on compressing low-value type boundaries and localizing config ownership, while preserving the operational safeguards that S-04 intentionally shipped.

## Confidence

**HIGH** - strong type-surface evidence, direct user narrowing signal, and cross-check evidence that the operational workflow safeguards are real behavior rather than speculative design.

## What Changes for /10x-plan

The plan should target readability and type-surface reduction in `LinuxGameCompat/Services/SummaryGeneration/`, plus focused test updates that stop locking unnecessary internal shape. It should explicitly preserve provider isolation, evidence hashing, concurrency locking, stale/failure lifecycle behavior, deterministic status precedence, and strict provider output validation.

## References

- Source files: `LinuxGameCompat/Services/SummaryGeneration/GenerationOrchestration.cs:9`, `LinuxGameCompat/Services/SummaryGeneration/GenerateSummariesCommand.cs:3`, `LinuxGameCompat/Services/SummaryGeneration/GenerationOptions.cs:7`, `LinuxGameCompat/Services/SummaryGeneration/ProviderContracts.cs:35`
- Tests: `LinuxGameCompat.Tests/PostgreSqlCompatibilityTests.cs:91`, `LinuxGameCompat.Tests/SummaryGenerationContractTests.cs:78`, `LinuxGameCompat.Tests/GenerateSummariesCommandTests.cs:28`
- Related roadmap: `context/foundation/roadmap.md:186`
- Investigation tasks: `019f056f-9d9c-7943-aa0c-a214f079f44d`, `019f056f-9e34-7c03-88c5-2e500c43797a`, `019f056f-9e63-7652-98ab-b02fd702a0e1`
