---
starter_id: blazor
package_manager: dotnet
project_name: linux-game-compat
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: best-effort
  path_taken: custom
  quality_override: true
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: true
  has_background_jobs: true
---

## Why this stack

The project is a solo after-hours MVP for a Linux game compatibility web app with auth, AI-backed summaries, and background processing needs. The user has commercial .NET experience and explicitly prefers a Blazor Web App over the registry's ASP.NET Core webapi starter, accepting that `starter_id: blazor` is custom bootstrapper contract override. Azure App Service, GitHub Actions, and auto-deploy-on-merge keep the deployment path aligned with the .NET ecosystem while preserving a straightforward hand-off for the next bootstrap step.
