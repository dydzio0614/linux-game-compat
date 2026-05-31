# LinuxGameCompat

LinuxGameCompat is an early-stage web app for checking Linux game compatibility from source-backed evidence. The goal is to help people switching to Linux quickly understand whether a game is likely to work, what caveats exist, and which external sources support those claims.

Primary reason of existence of this app is practicing AI-native software development workflow with heavy use of proprietary coding agent skills.

The project currently contains a .NET Blazor Server app backed by PostgreSQL. It includes the first anonymous game search flow, a compatibility evidence data model, seed data, Entity Framework Core migrations, and tests around compatibility data validation and PostgreSQL integration.

## Features

- Search curated games by title.
- Show compact normalized compatibility statuses in search results.
- Store source-backed compatibility evidence from systems such as ProtonDB and Are We Anti-Cheat Yet?
- Track source references, evidence claims, and generated compatibility summaries.
- Hide internal or suppressed game records from public lookup.

Planned MVP work includes game detail pages with source-linked evidence breakdowns, caveats, common workarounds, and member favorites.

## Tech Stack

- .NET 10
- Blazor Server / Razor Components
- Entity Framework Core
- PostgreSQL
- xUnit
- Testcontainers for PostgreSQL integration tests
- Docker for containerized deployment

## Repository Layout

```text
LinuxGameCompat/          Web application source
LinuxGameCompat.Tests/    Automated tests
context/                  Product, planning, and implementation notes
Dockerfile                Production container build
docker-compose.yml        Local PostgreSQL dependency
```

## Prerequisites

- .NET 10 SDK
- Docker or another local PostgreSQL 18-compatible database

For local development, the included Docker Compose file starts PostgreSQL on `127.0.0.1:5433` with the development credentials already referenced by `LinuxGameCompat/appsettings.Development.json`.

## Getting Started

Start PostgreSQL:

```bash
docker compose up -d
```

Restore dependencies:

```bash
dotnet restore LinuxGameCompat.sln
```

Apply database migrations:

```bash
dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj
```

Run the app:

```bash
dotnet run --project LinuxGameCompat/LinuxGameCompat.csproj
```

Then open the local URL printed by `dotnet run`.

## Configuration

The app reads the database connection from either:

- `ConnectionStrings:CompatibilityDatabase`
- `DATABASE_URL`

`DATABASE_URL` is useful for hosted environments. The app converts it to an Npgsql connection string and enables required SSL mode.

## Tests

Run the test suite with:

```bash
dotnet test LinuxGameCompat.sln
```

Some tests use Testcontainers and require Docker to be available.

## Deployment

The repository includes a multi-stage Dockerfile:

```bash
docker build -t linux-game-compat .
docker run -p 8080:8080 -e DATABASE_URL="postgres://user:password@host:5432/dbname" linux-game-compat
```

The container listens on `PORT` when provided, otherwise it defaults to `8080`.
