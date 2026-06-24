<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Recolor Blue Elements to Red

- **Plan**: `context/changes/recolor/plan.md`
- **Scope**: Full plan (CI review on PR #8)
- **Date**: 2026-06-24
- **CI run**: https://github.com/dydzio0614/linux-game-compat/actions/runs/28127730943
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Test Coverage | WARNING |
| Success Criteria | WARNING |

## Findings

### F1 — Normal link hover color does not take effect

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — Quick decision. Fix is obvious and narrowly scoped. Safe to batch.
- **Dimension**: Plan Adherence
- **Location**: LinuxGameCompat/wwwroot/app.css:18
- **Detail**: The plan required link color and link hover color to move to the crimson palette. `:root` defines `--bs-link-hover-color` and `--bs-link-hover-color-rgb`, but the later `a, .btn-link { color: #a61b2b; }` rule writes a direct color. Bootstrap's `a:hover` changes `--bs-link-color-rgb`, so normal links keep the non-hover crimson instead of using the darker hover shade.
- **Fix**: Add an explicit `a:hover, .btn-link:hover { color: #7f1320; }` rule, or remove the direct link color override and rely on the Bootstrap link variables.
- **Decision**: PENDING

### F2 — Accent search command is ambiguous as a pass/fail check

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — Quick decision. Fix is obvious and narrowly scoped. Safe to batch.
- **Dimension**: Success Criteria
- **Location**: context/changes/recolor/plan.md:85
- **Detail**: The declared `rg` verification command uses `--glob '!wwwroot/lib/**'` while searching from `LinuxGameCompat`, so it does not exclude `LinuxGameCompat/wwwroot/lib/**` in this repository. It also searches for `btn-primary` and `btn-outline-primary`, which are expected to remain in Razor markup and app-owned overrides. A corrected non-vendored search found no old app-owned blue hex values, so this is a verification clarity issue rather than an implementation blocker.
- **Fix**: Narrow the search to old blue color values and use `--glob '!LinuxGameCompat/wwwroot/lib/**'`, or run the search from `LinuxGameCompat/` with the existing vendored glob.
- **Decision**: PENDING

## Verification

- `dotnet build LinuxGameCompat.sln`: passed with 0 warnings and 0 errors.
- `dotnet test LinuxGameCompat.sln`: passed with 133 passed, 0 failed, 0 skipped.
- Plan `rg` command: returned expected primary class references and vendored Bootstrap matches because of the glob/path ambiguity noted in F2.
- Corrected non-vendored search: found expected primary class references and new app-owned overrides, with no old app-owned blue hex values.

<!-- End of report -->
