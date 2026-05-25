---
project: Linux Compatibility Aggregator
researched_at: 2026-05-25T00:00:00+02:00
recommended_platform: Railway
runner_up: Azure App Service
context_type: mvp
tech_stack:
  language: .NET / C#
  framework: Blazor Web App / ASP.NET Core
  runtime: Docker container on Railway
---

## Recommendation

**Deploy on Railway.**

Railway is the best MVP deployment fit for this project because it keeps monthly cost low, supports ASP.NET Core through Docker, has a strong CLI/logging surface, and provides official MCP support for agent-assisted operations. Azure App Service is the cleaner .NET-native option, but its realistic production setup depends on paid Always On tiers for reliable background work, which conflicts with the cost-sensitive MVP preference.

Primary evidence: [Railway ASP.NET Core guide](https://docs.railway.com/guides/aspnet-core), [Railway CLI deploy docs](https://docs.railway.com/cli/deploying), [Railway logs CLI](https://docs.railway.com/cli/logs), [Railway pricing](https://docs.railway.com/pricing/plans), [Railway remote MCP](https://docs.railway.com/ai/remote-mcp-server).

## Platform Comparison

| Platform | CLI-first | Managed / serverless | Agent-readable docs | Stable deploy API | MCP / integration | Fit for this stack | Total |
|---|---|---|---|---|---|---|---|
| Railway | Pass | Pass | Pass | Pass | Pass | Good, Docker required | 5 / 5 |
| Azure App Service | Pass | Pass | Pass | Pass | Pass | Excellent, higher cost | 5 / 5 |
| Render | Pass | Pass | Pass | Pass | Pass | Good, Docker required | 5 / 5 |
| Fly.io | Pass | Partial | Pass | Pass | Fail | Good, more ops-shaped | 3.5 / 5 |
| Cloudflare Workers / Pages | Pass | Pass | Pass | Pass | Pass | Poor for server-side Blazor | Dropped |
| Vercel | Pass | Pass | Pass | Pass | Partial | Poor for .NET server runtime | Dropped |
| Netlify | Pass | Pass | Pass | Pass | Pass | Poor for .NET server runtime | Dropped |

Railway supports ASP.NET Core deployment through Docker and offers `railway up`, GitHub autodeploys, project/environment tokens, `railway logs`, JSON log output, and official remote MCP. The main caveat is that .NET is not currently a zero-config Railpack target, so the project must own a Dockerfile.

Azure App Service is the strongest .NET/Blazor match. Microsoft documents Blazor App Service deployment, Azure CLI operations, WebJobs, log tailing, deployment slots, and Azure MCP App Service tooling. It is less attractive for this MVP because dependable background jobs need Always On, and Always On is available only on paid Basic/Standard/Premium-style tiers. App Service built-in MCP authorization is marked preview as of this research date.

Render can run the app as a Docker web service and can run separate background workers. It has a modern CLI, API rollback, hosted MCP, logs, metrics, and managed Postgres/Key Value options. It loses to Railway because .NET also requires Docker, pricing is less attractive for the selected low-cost preference, and its MCP docs note operational limitations around triggering deploys and some resource creation.

Fly.io is viable for a containerized .NET app and has excellent `flyctl` coverage for deploys, logs, scaling, secrets, releases, and machines. It is more infrastructure-shaped than Railway for a solo after-hours MVP, has no true free tier, lacks a first-party infrastructure-management MCP, and rollback requires redeploying a known previous image.

Cloudflare Workers/Pages, Vercel, and Netlify have strong agent ergonomics, but they are poor matches for the current server-side Blazor Web App architecture. Cloudflare Pages fits static Blazor WebAssembly, not Blazor Server/Web App hosting on ASP.NET Core. Vercel has no official .NET runtime for server functions. Netlify Functions support TypeScript, JavaScript, and Go, not .NET. These options would require redesigning the app into static Blazor WASM plus separate non-.NET backend functions.

Additional evidence: [Azure App Service Blazor quickstart](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore), [Azure WebJobs](https://learn.microsoft.com/en-us/azure/app-service/overview-webjobs), [Azure App Service pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/), [Render Docker docs](https://render.com/docs/docker), [Render MCP](https://render.com/docs/mcp-server), [Fly.io .NET docs](https://fly.io/docs/languages-and-frameworks/dotnet/), [Cloudflare Workers languages](https://developers.cloudflare.com/workers/languages/), [Vercel runtimes](https://vercel.com/docs/functions/runtimes), [Netlify Functions overview](https://docs.netlify.com/build/functions/overview/).

### Shortlisted Platforms

#### 1. Railway (Recommended)

Railway best matches the accepted constraints: cost-sensitive MVP, no persistent connection requirement, no platform familiarity tie-breaker, single-region traffic, and external managed services allowed. It gives a simple deploy path with `railway up`, strong logs through `railway logs`, and official remote MCP support, while keeping the operational surface smaller than Fly.io and cheaper to start than Azure App Service.

#### 2. Azure App Service

Azure App Service is the safest .NET ecosystem choice and aligns with the earlier `tech-stack.md` deployment hint. It loses narrowly because the MVP cost preference matters more than .NET-native hosting, and reliable continuous/scheduled WebJobs require Always On on paid tiers.

#### 3. Render

Render is a solid Docker-based alternative with web services, background workers, CLI, API, hosted MCP, and managed data products. It remains a fallback if Railway's Docker workflow, rollback model, or platform reliability becomes unacceptable.

## Anti-Bias Cross-Check: Railway

### Devil's Advocate - Weaknesses

1. ASP.NET Core on Railway requires a Dockerfile because Railway's current build path does not provide zero-config .NET support; bad Docker defaults can cause slow deploys, oversized images, or failed port binding.
2. Rollback is less scriptable than deploy/log operations. Railway documents rollback as a deployment action in the dashboard, with availability limited by plan retention.
3. Usage-based pricing can surprise a cost-sensitive MVP if logs, egress, build minutes, or background processing grow faster than expected.
4. Background AI summary generation may need a separate worker plus queue, which increases the number of Railway services and the operational model.
5. Railway is optimized for fast app deployment, not deep Azure-style .NET operations; diagnosing CLR/runtime problems may still fall back to container logs and local reproduction.

### Pre-Mortem - How This Could Fail

Six months after launch, the Railway choice failed because the team treated Docker support as equivalent to first-class .NET support. The initial Dockerfile worked locally but produced large images and slow cold starts. A background summarization worker was added without a queue budget or concurrency limits, so AI calls, logs, and egress grew unpredictably. Rollback looked simple during research, but the first bad release included a schema migration; reverting the deployment restored the image and variables but not the database state. The solo developer then had to debug production with only container logs while also learning Railway's service, environment, and billing model. By the time traffic increased, the platform was no longer the cheapest path, and the project needed a migration either to Azure App Service for .NET-native operations or to a more deliberate container setup.

### Unknown Unknowns

- Railway's ASP.NET Core guide currently depends on Docker; if the app later assumes framework-specific hosting features, those must be modeled explicitly in the container.
- Rollback does not remove the need for forward-only database migration discipline.
- Railway service boundaries matter: web app, background worker, queue, and database may need separate services with explicit environment variables and private networking.
- The official remote MCP is useful for agent-assisted operations, but production credentials still need scoped access and human approval for destructive actions.
- Recent platform incidents or support-quality concerns should be reviewed before treating Railway as production-critical infrastructure.

## Operational Story

- **Preview deploys**: Use Railway GitHub integration and PR environments where enabled; otherwise use a separate Railway environment for staging previews. Protect any preview URL that exposes member features or source-backed summaries.
- **Secrets**: Store app secrets as Railway service variables and provider secrets in their owning platforms. Do not commit secrets to the repo or `.mcp.json`. Use scoped Railway project/environment tokens for CI or agent access.
- **Rollback**: Use Railway deployment rollback from the service deployment history. Expect image and custom variables to roll back when the retained deployment is available; database migrations and external provider state do not roll back automatically.
- **Approval**: A human approves production publish, billing plan changes, primary secret rotation, database destructive actions, and any migration that cannot be safely rolled forward. The agent may run read-only checks, inspect logs, and prepare deploy commands.
- **Logs**: Use `railway logs`, `railway logs --build`, `railway logs --deployment`, `railway logs --lines 100`, and `railway logs --json` for read-only operational checks.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Dockerfile becomes the hidden production contract for .NET runtime behavior | Devil's advocate | Medium | High | Keep the Dockerfile minimal, pinned to official Microsoft .NET images, and verify port binding and release build locally before first deploy. |
| Rollback restores app image but not database state | Pre-mortem | Medium | High | Use forward-only migrations, backup before risky schema changes, and treat rollback as app-only unless a data rollback plan exists. |
| Usage-based costs exceed the intended cheap MVP profile | Devil's advocate | Medium | Medium | Add billing alerts where available, review Railway usage weekly during MVP, and cap worker concurrency and AI calls. |
| Background summary processing grows into a multi-service architecture too early | Unknown unknowns | Medium | Medium | Start with the smallest separate worker/queue shape only when needed; document service variables and ownership per service. |
| Platform reliability or support issues disrupt a solo after-hours workflow | Research finding | Medium | Medium | Keep Azure App Service as the runner-up escape route and avoid Railway-specific app code where possible. |
| Agent receives overly broad Railway credentials | Unknown unknowns | Low | High | Use scoped project/environment tokens, keep destructive actions human-only, and prefer read-only log/status checks for routine agent operations. |
| .NET diagnostics are weaker than on Azure-native hosting | Devil's advocate | Medium | Medium | Emit structured application logs to stdout/stderr and keep local Docker reproduction part of the deploy checklist. |

## Getting Started

1. Add a Dockerfile for the Blazor Web App using official Microsoft .NET SDK/runtime images, publishing in Release mode and binding ASP.NET Core to Railway's expected `PORT`.
2. Install Railway CLI and authenticate with the project account: `npm i -g @railway/cli`, then `railway login`.
3. Create or link the Railway project from the repo: `railway init` for a new project or `railway link` for an existing one.
4. Configure required service variables in Railway for auth, database, AI provider credentials, and application environment.
5. Deploy with `railway up`, then verify the deployment and runtime behavior with `railway logs --deployment`, `railway logs --lines 100`, and an HTTP smoke test against the Railway URL.

## Out of Scope

The following were not evaluated in this research:

- Docker image configuration
- CI/CD pipeline setup
- Production-scale architecture such as multi-region high availability or disaster recovery
