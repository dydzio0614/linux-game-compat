# Recolor Blue Elements to Red Implementation Plan

## Overview

Recolor the app's blue visual accents to an accessible crimson palette while preserving existing behavior, Razor markup, data model, and semantic state colors. The change is styling-only and should be implemented through app-owned CSS because `app.css` loads after Bootstrap.

## Current State Analysis

The app uses Bootstrap plus app-owned CSS. `LinuxGameCompat/Components/App.razor` loads Bootstrap first, then `app.css`, then isolated component styles, so app CSS can override Bootstrap primary styling without editing vendored files.

Several blue accents are hard-coded in app-owned styles: links, filled primary buttons, focus rings, status labels, summary advisory blocks, the sidebar gradient, and reconnect modal affordances. Razor pages also use `btn-primary` and `btn-outline-primary`, but the current global CSS only overrides filled `.btn-primary`, leaving outline-primary behavior mostly controlled by Bootstrap.

## Desired End State

The UI no longer presents blue as the app accent color in app-owned normal surfaces. Links, primary buttons, outline-primary buttons, focus rings, compatibility status chips, summary advisory blocks, sidebar navigation background, and reconnect modal accents use a coherent crimson palette with readable contrast.

Semantic colors remain meaningful: validation red, warning yellow, success green, and existing Blazor error boundary styling are not broadly recolored. No routes, services, Razor behavior, database model, or migrations change.

### Key Discoveries:

- `LinuxGameCompat/Components/App.razor:9` loads Bootstrap before `app.css`, allowing app-level overrides without vendor edits.
- `LinuxGameCompat/wwwroot/app.css:5` contains the global link, button, focus, status label, and advisory blue accents.
- `LinuxGameCompat/Components/Layout/MainLayout.razor.css:11` contains the blue-to-purple sidebar gradient.
- `LinuxGameCompat/Components/Layout/ReconnectModal.razor.css:91` contains reconnect modal blue button and animation styling.
- `LinuxGameCompat/Components/Pages/Home.razor:25`, `Games.razor:44`, `Favorites.razor:27`, `GameDetail.razor:49`, and `Login.razor:48` show current Bootstrap primary/outline-primary usage that should be covered by CSS overrides.

## What We're NOT Doing

- Not editing vendored Bootstrap files under `LinuxGameCompat/wwwroot/lib`.
- Not changing Razor markup classes unless CSS overrides cannot cover a specific primary case.
- Not changing app behavior, authentication, favorites, summary generation, data access, schema, or migrations.
- Not recoloring semantic warning, validation, success, or existing error-boundary states solely for palette uniformity.
- Not adding screenshot automation or a browser test harness for this MVP visual change.

## Implementation Approach

Use `app.css` as the theme override layer for global Bootstrap primary behavior and app-specific blue accents. Then update isolated component CSS where blue is local to layout or reconnect UI. This keeps the change small, avoids vendor churn, and lets existing Razor markup keep using Bootstrap's primary class vocabulary.

## Phase 1: Theme Accent Recolor

### Overview

Replace app-owned blue accents with an accessible crimson palette while preserving semantic state colors and existing component behavior.

### Changes Required:

#### 1. Global App Theme Overrides

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Establish crimson as the app's primary accent after Bootstrap loads. This should cover existing and future uses of Bootstrap primary/link/focus tokens without modifying vendored Bootstrap.

**Contract**: Add or update app-level primary CSS variable overrides in `:root` for Bootstrap primary color, primary RGB, link color, link hover color, focus ring color, and primary subtle text/background/border tokens. Values must be accessible crimson shades, not bright destructive-only red.

#### 2. Global App Blue Accent Replacement

**File**: `LinuxGameCompat/wwwroot/app.css`

**Intent**: Replace hard-coded blue accents in app-owned surfaces with the same crimson palette so normal pages no longer read visually blue.

**Contract**: Update `a, .btn-link`, `.btn-primary`, focus ring styling, `.status-label`, and `.summary-advisory` colors. Add explicit `.btn-outline-primary` styling, including hover/active/focus/disabled behavior as needed, so current Razor usage renders red instead of Bootstrap blue. Preserve validation, warning, success, and Blazor error-boundary colors.

#### 3. Sidebar Accent Recolor

**File**: `LinuxGameCompat/Components/Layout/MainLayout.razor.css`

**Intent**: Recolor the sidebar background from blue/purple to a crimson/wine gradient while keeping existing layout and nav readability.

**Contract**: Update `.sidebar` `background-image` only. Keep existing sizing, sticky behavior, and top-row styling unchanged unless contrast requires a minimal color adjustment.

#### 4. Reconnect Modal Accent Recolor

**File**: `LinuxGameCompat/Components/Layout/ReconnectModal.razor.css`

**Intent**: Ensure Blazor reconnect UI no longer shows blue controls or blue animation rings when connection state changes.

**Contract**: Update reconnect modal button normal/hover/active colors and `.components-rejoining-animation div` border color to crimson-compatible shades. Keep display logic, animation names, timing, layout, and dialog behavior unchanged.

### Success Criteria:

#### Automated Verification:

- App builds successfully: `dotnet build LinuxGameCompat.sln`
- Test suite passes when environment prerequisites are available: `dotnet test LinuxGameCompat.sln`
- App-owned CSS/Razor search shows no unintended blue primary accents outside vendored Bootstrap: `rg -n "#(006bb7|1b6ec2|1861ac|258cfb|e7f0ff|1849a9|528bff|eef4ff|052767|6b9ed2|3b6ea2|0087ff)|rgb\\(5, 39, 103\\)|btn-outline-primary|btn-primary" LinuxGameCompat --glob '!wwwroot/lib/**'`

#### Manual Verification:

- Review the changed CSS and confirm semantic warning, validation, success, and existing error-boundary colors were not broadly recolored.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual review was successful before proceeding to the next phase. Phase blocks use plain bullets; the corresponding checkboxes live in the `## Progress` section.

---

## Phase 2: Build Verification

### Overview

Run the standard project verification commands and record whether any failures are environmental rather than caused by the recolor.

### Changes Required:

#### 1. Verification Run

**File**: `context/changes/recolor/implementation-notes.md`

**Intent**: Capture verification outcomes for the styling-only change so future reviewers can distinguish code failures from local Docker/Testcontainers limitations.

**Contract**: If verification is performed during implementation, create or update implementation notes with the commands run, pass/fail outcome, and any environment limitation. Do not mark a Testcontainers/Docker prerequisite failure as a CSS implementation failure.

### Success Criteria:

#### Automated Verification:

- Build result is recorded after running `dotnet build LinuxGameCompat.sln`
- Test result is recorded after running `dotnet test LinuxGameCompat.sln`, or Docker/Testcontainers limitation is recorded if tests cannot run fully

#### Manual Verification:

- Human confirms the build/test outcome is acceptable for this styling-only change.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the verification result is acceptable.

---

## Testing Strategy

### Unit Tests:

- No new unit tests are required because the planned changes are CSS-only.
- Existing xUnit tests should still pass because no app behavior or data contracts change.

### Integration Tests:

- Run the existing solution test suite with `dotnet test LinuxGameCompat.sln` when Docker/Testcontainers prerequisites are available.

### Manual Testing Steps:

1. Review `app.css` for the crimson palette and ensure old app-owned blue accent hex values were replaced.
2. Review `MainLayout.razor.css` and `ReconnectModal.razor.css` for remaining app-owned blue accents.
3. Confirm semantic colors were preserved for validation, warning, success, and existing error-boundary states.

## Performance Considerations

No performance impact is expected. The change only swaps CSS values and does not add assets, scripts, layout calculations, or runtime behavior.

## Migration Notes

No database, configuration, or deployment migration is required.

## References

- Change request: `context/changes/recolor/change.md`
- Bootstrap load order: `LinuxGameCompat/Components/App.razor:9`
- Global app styling: `LinuxGameCompat/wwwroot/app.css:5`
- Sidebar styling: `LinuxGameCompat/Components/Layout/MainLayout.razor.css:11`
- Reconnect modal styling: `LinuxGameCompat/Components/Layout/ReconnectModal.razor.css:91`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Theme Accent Recolor

#### Automated

- [x] 1.1 App builds successfully: `dotnet build LinuxGameCompat.sln` — ed21944
- [x] 1.2 Test suite passes when environment prerequisites are available: `dotnet test LinuxGameCompat.sln` — ed21944
- [x] 1.3 App-owned CSS/Razor search shows no unintended blue primary accents outside vendored Bootstrap — ed21944

#### Manual

- [x] 1.4 Review the changed CSS and confirm semantic warning, validation, success, and existing error-boundary colors were not broadly recolored — ed21944

### Phase 2: Build Verification

#### Automated

- [x] 2.1 Build result is recorded after running `dotnet build LinuxGameCompat.sln`
- [x] 2.2 Test result is recorded after running `dotnet test LinuxGameCompat.sln`, or Docker/Testcontainers limitation is recorded if tests cannot run fully

#### Manual

- [x] 2.3 Human confirms the build/test outcome is acceptable for this styling-only change
