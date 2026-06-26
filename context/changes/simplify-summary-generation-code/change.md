---
change_id: simplify-summary-generation-code
title: Simplify summary generation code
status: implementing
created: 2026-06-26
updated: 2026-06-26
archived_at: null
---

## Notes

S-08 from context/foundation/roadmap.md - brief description: S-04 code is currently overengineered with too many of different datatypes / records. The code should be easy to understand rather than being "extendable for future use cases". Automated tests should also not drive design to be much more complicated than needed. Also config files for default parameters should be source of truth - in past there were excessive checks for "exceeding value caps" etc. that hardcoded default config values. Some excessive checks might be still there, together with possibly awkward class design that could benefit from that.
