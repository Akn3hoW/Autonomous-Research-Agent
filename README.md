# Autonomous Research Agent API

Production-structured v1 middle API for a scientific paper intelligence platform. This repository is designed as a modular monolith so the API contract can stay stable while ingestion logic, search, workers, and analysis capabilities evolve behind it.

## Architecture overview

The solution is organized into four projects:

- `src/Api`
  - ASP.NET Core Web API host
  - Controllers, request/response contracts, validation, auth, middleware, OpenAPI
- `src/Application`
  - Use-case models, service interfaces, exceptions, orchestration-facing contracts
- `src/Domain`
  - Core entities and enums
- `src/Infrastructure`
  - EF Core persistence, Npgsql/PostgreSQL integration, Semantic Scholar client, placeholder summarization/embedding services, job runner stub

Dependency direction:

- `Api -> Application`
- `Infrastructure -> Application + Domain`
- `Application -> Domain`
- `Domain -> nothing`

This keeps API DTOs from leaking inward and allows the frontend or future AI admin tools to depend on OpenAPI contracts rather than EF entities or internal schemas.

## Solution structure

```text
AutonomousResearchAgent.sln
src/
  Api/
    Authorization/
    Controllers/
    Contracts/
    Extensions/
    Middleware/
    OpenApi/
    Program.cs
  Application/
    Analysis/
    Common/
    Jobs/
    Papers/
    Search/
    Summaries/
  Domain/
    Entities/
    Enums/
  Infrastructure/
    BackgroundJobs/
    External/
    Extensions/
    Persistence/
    Services/
```

## Key design choices

### Stable API contract

- Controllers expose DTO-based contracts only.
- EF entities are never serialized directly.
- `PATCH` endpoints use partial update request models instead of replacement semantics.
- ProblemDetails is the standard error envelope.

### Hybrid structured + flexible storage

- Indexed/filterable fields live in first-class columns.
- LLM outputs, job payloads/results, and analysis results live in `jsonb`.
- Embeddings stay domain-friendly as `float[]` while the infrastructure layer maps them to a PostgreSQL `vector(1536)` column so future pgvector similarity operators can slot in without changing the public API.

### Job and analysis readiness

- Jobs are durable records with clear type/status lifecycle fields.
- `IJobService` represents API-facing job management.
- `IJobRunner` is separate so future hosted services or external workers can execute the same job model.
- Analysis supports synchronous comparison endpoints plus an async insights job pattern.

### Auth readiness

- JWT bearer auth is wired now.
- Policies are grouped by access intent:
  - `ReadAccess`
  - `EditAccess`
  - `ReviewAccess`
  - `AdminAccess`
- Roles expected by default:
  - `Admin`
  - `Editor`
  - `Reviewer`
  - `ReadOnly`

## Main capabilities in v1

### Papers

- `GET /api/v1/papers`
- `GET /api/v1/papers/{id}`
- `POST /api/v1/papers`
- `PATCH /api/v1/papers/{id}`
- `POST /api/v1/papers/import`

### Summaries

- `GET /api/v1/papers/{id}/summaries`
- `POST /api/v1/papers/{id}/summaries`
- `GET /api/v1/summaries/{summaryId}`
- `PATCH /api/v1/summaries/{summaryId}`
- `POST /api/v1/summaries/{summaryId}/approve`
- `POST /api/v1/summaries/{summaryId}/reject`

### Search

- `GET /api/v1/search?q=...`
- `POST /api/v1/search/semantic`
- `POST /api/v1/search/hybrid`

### Jobs

- `GET /api/v1/jobs`
- `GET /api/v1/jobs/{id}`
- `POST /api/v1/jobs/import-papers`
- `POST /api/v1/jobs/summarize-paper`
- `POST /api/v1/jobs/{id}/retry`

### Analysis

- `POST /api/v1/analysis/compare-papers`
- `POST /api/v1/analysis/compare-fields`
- `POST /api/v1/analysis/generate-insights`
- `GET /api/v1/analysis/{jobId}`

## Local development

### Prerequisites

- .NET 9 SDK
- PostgreSQL 15+
- pgvector extension enabled in the target database

### Restore and run

```bash
dotnet restore
dotnet run --project src/Api
```

OpenAPI document will be available at:

```text
http://localhost:5000/openapi/v1.json
```

Health endpoint:

```text
https://localhost:5001/health
```

## Database and migrations

The DbContext lives in `src/Infrastructure/Persistence/ApplicationDbContext.cs`. A design-time factory is included so EF Core tooling can create migrations.

Example commands:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Infrastructure \
  --startup-project src/Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Infrastructure \
  --startup-project src/Api
```

## Configuration

Key configuration sections:

- `ConnectionStrings:Postgres`
- `Jwt`
- `SemanticScholar`

`src/Api/appsettings.json` includes development-safe starter values. Replace the signing key, issuer, audience, connection string, and Semantic Scholar API key before production use.

## Extension points

### Replace placeholder AI services

- `IEmbeddingService`
- `ISummarizationService`

Swap the placeholder implementations in `Infrastructure/Services` for real provider-backed services without changing the API surface.

### Add real background execution

- Keep using the `jobs` table and `IJobService`
- Replace or extend `NoOpJobRunner`
- Add hosted services or external worker processes that consume the same durable job records

### Improve search quality

- Replace ILIKE keyword search with PostgreSQL full-text search if needed
- Move semantic ranking from in-memory placeholder logic to pgvector distance queries
- Tune hybrid ranking weights and retrieval windows without changing search DTOs

### Grow analysis workflows

- Enrich JSONB result schemas with typed helper models in `Application`
- Add new analysis types without breaking existing endpoints
- Introduce scheduled or multi-step analysis pipelines through the job model

## Notes

- The current code is intentionally realistic but lightweight: it is a strong v1 foundation, not a finished production system.
- This environment did not have the .NET SDK installed while generating the repository, so the structure was authored to be restore/build-ready but was not compiled in-place here.
