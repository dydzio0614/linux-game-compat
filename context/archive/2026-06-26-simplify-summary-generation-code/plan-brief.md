# Simplify Summary Generation Code - Plan Brief

> Full plan: `context/changes/simplify-summary-generation-code/plan.md`
> Frame brief: `context/changes/simplify-summary-generation-code/frame.md`
> Research: `context/archive/2026-06-14-generated-compatibility-synthesis/research.md`

## What & Why

S-04 summary generation is hard to read because one finite MVP command is spread across too many small boundary types and seams, while some remaining config/default validation duplication adds secondary noise.

This plan simplifies the implementation surface without treating the whole generator workflow as overengineering.

## Starting Point

The current code already has the production safeguards S-04 needed: provider isolation, strict output validation, full evidence hashing, advisory locking, stale/failure lifecycle behavior, and deterministic status precedence. The main friction is local type surface around command parsing, generator resolution, candidate state, and config defaults.

## Desired End State

Maintainers can trace the generation command from `Program.cs` through orchestration to provider call without unnecessary adjacent DTO/interface handoffs. Generation defaults are owned by `appsettings.json`, tests protect behavior rather than internal shape, and all safety behavior remains intact.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Refactor scope | Broader flattening of local seams | User selected a more visible cleanup, but provider/evidence contracts stay because they carry real safety meaning. | Plan |
| Config ownership | Config owns defaults | The frame called duplicated code/config defaults secondary noise. | Frame / Plan |
| Test strategy | Behavior-focused tests | Tests should preserve production guarantees without forcing unnecessary design seams. | Frame / Plan |
| Operational safeguards | Preserve unchanged | Prior S-04 research and tests show these are behavior-bearing, not speculative. | Research / Frame |

## Scope

**In scope:**

- Remove the one-implementation generator interface.
- Collapse the command-options/run-options handoff.
- Remove unused candidate evidence storage.
- Reduce duplicated default ownership in `GenerationOptions`.
- Update tests and docs to match the simplified behavior surface.

**Out of scope:**

- Database migrations.
- UI/read-model changes.
- Provider API, prompt schema, output validation, hashing, status-mapping, or Railway rollout changes.
- Removing behavior-bearing PostgreSQL safety tests.

## Architecture / Approach

This is a bounded readability refactor inside the existing summary-generation feature folder. The generator remains the orchestration boundary, the provider remains isolated behind its interface, and prompt/evidence/provider contracts remain explicit. The low-value seams between CLI parsing, run options, DI, and internal candidate state are compressed.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Flatten Internal Orchestration Seams | Removes one-caller interface, duplicate options handoff, and unused candidate field. | Accidentally changing command or selection behavior. |
| 2. Localize Config Defaults and Validation | Makes `appsettings.json` the clear default source while keeping real validation guardrails. | Weakening cost/safety validation too much. |
| 3. Behavior-Focused Test and Documentation Cleanup | Aligns tests/docs with the simplified surface. | Pruning tests that protect real production behavior. |

**Prerequisites:** Current baseline tests are green.
**Estimated effort:** One focused implementation session across three small phases.

## Open Risks & Assumptions

- Broader flattening means removing local low-value seams, not inlining provider/prompt/output-validation contracts.
- Config validation should still reject unsafe generation-mode settings even if exact defaults move out of code.
- README changes may be unnecessary if command/config documentation remains accurate.

## Success Criteria (Summary)

- Summary-generation behavior remains unchanged under the existing unit and PostgreSQL integration tests.
- The code has fewer internal DTO/interface handoffs and no unused candidate evidence field.
- `appsettings.json` is the obvious source for generation defaults, with tests focused on behavior and invariants.
