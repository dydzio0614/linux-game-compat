# First Railway Deployment Plan With Postgres

## Summary

Deploy the current .NET 10 Blazor Web App to Railway using the Railway CLI, and provision a Railway Postgres database in the same project for upcoming persistence work.

Railway is the accepted platform from `context/foundation/infrastructure.md`; the older Azure hint in `context/foundation/tech-stack.md` is superseded. The first deploy will not add app database code yet.

## Manual Actions You Need To Perform

- Create or log in to a Railway account at `https://railway.com/`.
- Add a payment method or enable the required Railway plan if Railway asks for it.
- During `railway login`, complete the browser login flow.
- During Railway project setup, create/select project `linux-game-compat`.
- Add a Postgres database service in Railway:
  - Project -> `New` -> `Database` -> `Postgres`
  - Use the same environment as the web app, usually `production`
- Confirm the web app service receives Railway's database connection variable.
  - Preferred future app variable: `DATABASE_URL`
  - Do not paste the value into chat or commit it to files.
- After deployment, open the Railway public URL and confirm the site loads.

## Key Repo Changes

- Create root `first-deployment-plan.md` containing this deployment plan before implementation begins.
- Add a root `Dockerfile` for Railway container deployment:
  - Build with official Microsoft .NET 10 SDK image.
  - Run with official Microsoft ASP.NET Core 10 runtime image.
  - Publish `LinuxGameCompat/LinuxGameCompat.csproj` in Release mode.
  - Bind to Railway's runtime `PORT`, with `8080` fallback for local testing.
- Add a root `.dockerignore` excluding `bin/`, `obj/`, `.git/`, IDE folders, and local-only files.
- Do not add EF Core, migrations, schema, or database health-check code in this first deploy.

## Implementation And Deploy Steps

1. Write `first-deployment-plan.md`.

2. Add Docker deployment files.

3. Verify the app builds:

   ```bash
   dotnet restore LinuxGameCompat.sln
   dotnet publish LinuxGameCompat/LinuxGameCompat.csproj -c Release
   ```

4. If Docker is installed, verify the container locally:

   ```bash
   docker build -t linux-game-compat:local .
   docker run --rm -p 8080:8080 -e PORT=8080 -e ASPNETCORE_ENVIRONMENT=Production linux-game-compat:local
   ```

   Open `http://localhost:8080`.

5. Install Railway CLI if missing:

   ```bash
   npm i -g @railway/cli
   ```

6. Log in:

   ```bash
   railway login
   ```

   Manual gate: complete browser authentication.

7. Create or link Railway project:

   ```bash
   railway init
   ```

   Choose/create `linux-game-compat`.

   If the project already exists:

   ```bash
   railway link
   ```

8. Add Railway Postgres manually in the dashboard.

9. Set initial web app variables:

   ```bash
   railway variables set ASPNETCORE_ENVIRONMENT=Production
   railway variables set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
   ```

10. Deploy:

    ```bash
    railway up
    ```

11. Ensure a public domain exists:

    ```bash
    railway domain
    ```

    If no domain exists, create one in the Railway dashboard for the web service.

12. Verify production:

    ```bash
    railway status
    railway logs --lines 100
    ```

    Browser checks:
    - Home page loads.
    - Counter page works.
    - Weather page loads.
    - Unknown route shows not-found page.
    - Logs show no crash loop or port binding error.

## Test Plan

- `dotnet restore LinuxGameCompat.sln`
- `dotnet publish LinuxGameCompat/LinuxGameCompat.csproj -c Release`
- `docker build -t linux-game-compat:local .` if Docker is available
- Local browser smoke test at `http://localhost:8080` if Docker is available
- Railway Postgres service exists in the same project/environment
- `railway up`
- Production browser smoke test on the Railway URL
- `railway logs --lines 100`

## Assumptions

- Database choice is Railway-managed Postgres.
- This deploy provisions the database but does not use it in app code yet.
- No schema or migrations are created during first deployment.
- You will handle Railway account, billing, browser login, and dashboard database creation manually.
- Secrets and database URLs stay in Railway variables only, never in repo files or chat.
