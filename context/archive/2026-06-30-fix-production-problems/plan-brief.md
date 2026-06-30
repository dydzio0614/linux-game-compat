# Fix Production Catalog Interactivity — Plan Brief

> Full plan: `context/changes/fix-production-problems/plan.md`
> Frame brief: `context/changes/fix-production-problems/frame.md`

## What & Why

The production deployment does not serve the Blazor browser bootstrap asset, so Interactive Server components remain prerendered HTML and cannot process catalog pagination or search events. The plan restores the missing .NET 10 web assets and makes that runtime contract a deployment gate.

## Starting Point

Production serves application assets and the Blazor hub but returns 404 for framework scripts. The current Dockerfile reproduces this behavior because its SDK 10.0.301 publish omits physical Blazor framework assets even though the manifest advertises their routes.

## Desired End State

Search and pagination invoke their existing server handlers in production. GitHub verifies every Release publish, and Railway refuses to promote any new image that cannot serve `/_framework/blazor.web.js`.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Problem boundary | Fix shared Blazor bootstrap failure | Both symptoms require interactivity and their backend handlers are valid | Frame |
| Asset inclusion | Set `RequiresAspNetWebAssets=true` | .NET 10 conditionally imports the framework assets during restore | Research |
| Container inputs | Pin SDK 10.0.301 and runtime 10.0.9 | Prevent floating tags from silently changing the publish toolchain | Plan |
| Pre-deploy gate | Publish inspection in GitHub Actions | Matches the requested lightweight CI contract | Plan |
| Deployment authority | Keep Railway GitHub autodeploy | Preserves the existing operational workflow | Plan |
| Promotion gate | Bootstrap URL as Railway healthcheck | Prevents traffic switching until the failed boundary works | Plan |
| Failure handling | Reject deployment; no automatic rollback | Keeps the prior active deployment without adding rollback automation | Plan |

## Scope

**In scope:**

- Explicit framework-asset inclusion and reproducible Docker inputs.
- Docker build assertions and runtime asset verification.
- GitHub pull-request/push verification.
- Railway config-as-code, `Wait for CI`, and guarded production rollout.
- End-to-end production checks for search and pagination.

**Out of scope:**

- Search or pagination redesign.
- Database changes.
- Continuous monitoring and automatic rollback.
- Replacing Railway autodeploy with a CLI deployment workflow.

## Architecture / Approach

The project file makes Blazor framework assets an explicit publish input. The Docker build validates the resulting artifact. GitHub repeats the publish verification before Railway autodeploy, while Railway checks the actual framework URL inside the candidate deployment before switching production traffic.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Restore asset contract | Reproducible image serving Blazor bootstrap assets | Explicit asset property must correct the reproduced SDK omission |
| 2. Gate autodeploys | CI and Railway promotion checks plus production rollout | Railway `Wait for CI` requires one-time dashboard confirmation |

**Prerequisites:** Docker, .NET 10, access to the existing Railway production service, and permission to change its `Wait for CI` setting.

**Estimated effort:** Two focused implementation sessions plus production verification.

## Open Risks & Assumptions

- The explicit .NET 10 web-assets property is expected to correct the reproduced omission; Phase 1 blocks rollout until the container proves it.
- Railway's healthcheck is deployment-time protection, not continuous monitoring.
- The current production deployment remains the fallback during the healthcheck, although it already lacks interactivity.

## Success Criteria Summary

- Production serves the Blazor bootstrap script and both reported controls work interactively.
- GitHub rejects revisions missing the publish asset contract.
- Railway does not promote a deployment whose bootstrap URL fails.
