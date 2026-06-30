# Fix Production Catalog Interactivity Implementation Plan

## Overview

Restore the Blazor framework assets omitted from the production Docker publish, then prevent Railway GitHub autodeploys from promoting an image that cannot bootstrap Interactive Server components. Search and pagination remain unchanged because both failures are consequences of the missing browser bootstrap script.

## Current State Analysis

Production serves prerendered pages, application static assets, and the Blazor hub, but returns HTTP 404 for both `/_framework/blazor.web.js` and `/_framework/blazor.server.js`. The same failure reproduces from the current Dockerfile: its SDK 10.0.301 publish advertises the bootstrap route in the static-assets manifest but omits the physical framework files from the final image. A host publish using SDK 10.0.109 includes and serves those files.

.NET 10 stopped embedding the Blazor scripts and uses an automatically imported web-assets package during restore. Explicitly setting `RequiresAspNetWebAssets` is the supported way to require these assets when automatic project detection does not include them.

## Desired End State

The production bootstrap URL returns JavaScript with HTTP 200, search and pagination invoke their existing server handlers, and every new production deployment is gated twice: GitHub CI verifies the publish contract, and Railway verifies the bootstrap URL before switching traffic.

### Key Discoveries

- Both affected pages require Interactive Server, while their existing handlers and database queries are independently valid (`LinuxGameCompat/Components/Pages/Home.razor:2`, `LinuxGameCompat/Components/Pages/Games.razor:2`).
- The Docker image reproduces production exactly: application assets return 200, but `/_framework/blazor.web.js` returns 404 (`Dockerfile:1`, `LinuxGameCompat/Program.cs:141`).
- Railway can wait for GitHub push workflows and can use the bootstrap URL itself as a pre-promotion healthcheck.

## What We're NOT Doing

- Redesigning pagination, search, or their database queries.
- Adding database migrations or changing stored data.
- Adding continuous uptime monitoring or automated rollback.
- Replacing Railway GitHub autodeploy with a Railway CLI deployment workflow.
- Adding general-purpose static-file fallback middleware unless explicit web-asset inclusion fails verification.

## Implementation Approach

Make the framework asset an explicit build requirement, freeze the SDK/runtime versions that produce the image, and fail the Docker build if the asset contract is absent. Add a GitHub push/PR verification workflow and repository-owned Railway configuration so a bad publish is rejected before deployment and a bad runtime image is rejected before receiving production traffic.

## Critical Implementation Details

### Timing & lifecycle

Railway's `Wait for CI` setting must be enabled after the push workflow exists. The bootstrap healthcheck must be committed before the corrective deployment so Railway validates the new container before replacing the currently active deployment.

## Phase 1: Restore and Lock the Framework Asset Contract

### Overview

Correct the .NET 10 publish inputs and prove the resulting production image serves the framework bootstrap script.

### Changes Required

#### 1. Web project publish contract

**File**: `LinuxGameCompat/LinuxGameCompat.csproj`

**Intent**: Explicitly require ASP.NET web assets so Blazor framework scripts are included regardless of SDK automatic detection behavior.

**Contract**: Add `RequiresAspNetWebAssets` with value `true` in the main property group; do not alter render modes or application routes.

#### 2. Reproducible production image

**File**: `Dockerfile`

**Intent**: Remove floating feature-band behavior and make a missing framework asset a build failure rather than a runtime incident.

**Contract**: Pin the build stage to `mcr.microsoft.com/dotnet/sdk:10.0.301` and runtime stage to `mcr.microsoft.com/dotnet/aspnet:10.0.9`. After publish, assert that the bootstrap asset exists and that `LinuxGameCompat.staticwebassets.endpoints.json` contains the `_framework/blazor.web.js` route before copying into the runtime stage.

### Success Criteria

#### Automated Verification

- Solution tests pass: `dotnet test LinuxGameCompat.sln`.
- Production image builds: `docker build -t linux-game-compat:asset-fix .`.
- Final image contains the bootstrap script and its manifest route.
- A running production image returns HTTP 200 with JavaScript content for `/_framework/blazor.web.js` and HTTP 200 for an application static asset.

#### Manual Verification

- Local container search submits interactively and displays matching games.
- Local container Next and Previous controls change the visible catalog page.

**Implementation Note**: Pause after automated verification and local browser confirmation before configuring the production deployment gates.

---

## Phase 2: Gate Railway GitHub Autodeploys

### Overview

Add pre-deployment publish checks and a Railway pre-promotion runtime healthcheck, then deploy and verify the two reported user flows.

### Changes Required

#### 1. GitHub publish verification

**File**: `.github/workflows/production-deploy-gate.yml`

**Intent**: Give Railway a push-based CI result to wait for and reject revisions whose Release publish lacks the Blazor asset contract.

**Contract**: Run for pull requests and pushes to `master`; use SDK 10.0.301; restore, test, and publish the web project; assert the bootstrap file and exact manifest route exist. Grant read-only repository permissions and use no production secrets.

#### 2. Railway deployment configuration

**File**: `railway.json`

**Intent**: Keep the production deployment behavior reviewable in source and prevent traffic promotion until Interactive Server can bootstrap.

**Contract**: Select the root Dockerfile builder and configure `/_framework/blazor.web.js` as the deployment healthcheck path with a bounded timeout. Preserve the Dockerfile entrypoint, existing port behavior, restart policy, and environment variables.

#### 3. Platform configuration and rollout

**Intent**: Connect the repository checks to the existing Railway GitHub integration and perform the guarded corrective deployment.

**Contract**: Enable Railway `Wait for CI` for the production service after the push workflow is present. Confirm the connected branch is `master`, deploy through the existing integration, and inspect the deployment/build logs without changing secrets or database configuration.

### Success Criteria

#### Automated Verification

- GitHub workflow succeeds on the corrective revision and exposes a push check Railway can wait for.
- A failing push workflow causes the corresponding Railway deployment to be skipped.
- Railway marks the corrective deployment active only after `/_framework/blazor.web.js` returns HTTP 200.
- Production bootstrap URL returns HTTP 200 with JavaScript content.

#### Manual Verification

- Production search submits without browser navigation and returns usable results.
- Production catalog Next and Previous controls update the displayed records.
- Browser developer tools show no bootstrap-load or Blazor circuit errors.

**Implementation Note**: Production rollout requires human confirmation of the Railway `Wait for CI` setting and the final browser smoke tests.

---

## Testing Strategy

### Unit and Build Tests

- Run the existing solution suite to detect application regressions.
- Verify Release publish output contains the physical bootstrap asset and manifest route.
- Verify the Docker build fails when its asset assertion is not satisfied.

### Integration Tests

- Run the production Docker image and probe the framework bootstrap, an application asset, and the Blazor hub boundary.
- Confirm Railway's healthcheck blocks promotion when the bootstrap path is unavailable.

### Manual Testing Steps

1. Open the deployed home page, enter a known partial title, submit, and confirm results render without a document-level GET navigation.
2. Open `/games`, click Next, confirm the displayed range and records change, then click Previous and confirm the first page returns.
3. Inspect the browser network/console for a successful bootstrap request, an established Blazor connection, and no relevant errors.

## Performance Considerations

The added CI checks operate only during pull requests and pushes. The Railway healthcheck downloads one framework script once per deployment and does not add continuous production traffic.

## Migration Notes

No database migration is required. Rollback is the previous Railway deployment, but it remains functionally broken until an image containing the framework assets is promoted; therefore Phase 1 verification is mandatory before rollout.

## References

- Frame brief: `context/changes/fix-production-problems/frame.md`
- .NET 10 asset behavior: `https://github.com/dotnet/aspnetcore/issues/64381`
- Railway autodeploy gating: `https://docs.railway.com/deployments/github-autodeploys`
- Railway healthchecks: `https://docs.railway.com/deployments/healthchecks`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Restore and Lock the Framework Asset Contract

#### Automated

- [x] 1.1 Solution tests pass — 2f26344
- [x] 1.2 Production image builds — 2f26344
- [x] 1.3 Final image contains the bootstrap script and manifest route — 2f26344
- [x] 1.4 Running image serves framework and application assets — 2f26344

#### Manual

- [x] 1.5 Local container search works interactively — 2f26344
- [x] 1.6 Local container pagination works interactively — 2f26344

### Phase 2: Gate Railway GitHub Autodeploys

#### Automated

- [x] 2.1 GitHub push workflow succeeds — 4a7ad77
- [x] 2.2 Failed CI causes Railway deployment to be skipped — 4a7ad77
- [x] 2.3 Railway healthcheck gates deployment activation — 4a7ad77
- [x] 2.4 Production bootstrap asset returns JavaScript successfully — 4a7ad77

#### Manual

- [x] 2.5 Production search works interactively — 4a7ad77
- [x] 2.6 Production pagination works interactively — 4a7ad77
- [x] 2.7 Browser reports no bootstrap or circuit errors — 4a7ad77
