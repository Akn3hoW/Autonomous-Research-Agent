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
  - EF Core persistence, Npgsql/PostgreSQL integration, Semantic Scholar client, OpenRouter summarization, local OCR/document extraction, local embedding client, background job runner

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
- Embeddings stay domain-friendly as `float[]` while the infrastructure layer maps them to a PostgreSQL `vector(768)` column for the local Snowflake Arctic embedding model.

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
- PostgreSQL 15+ with pgvector extension
- `ocrmypdf` for PDF OCR fallback
- Python 3.11+ for the local embedding service

### Installing ocrmypdf

`ocrmypdf` is required for PDF OCR when native PDF text extraction is insufficient.

**Ubuntu/Debian:**
```bash
sudo apt install ocrmypdf
```

**macOS:**
```bash
brew install ocrmypdf
```

**Python package (alternative):**
```bash
pip install ocrmypdf
```

Verify installation:
```bash
ocrmypdf --version
```

### Local database setup

1. **Install PostgreSQL 15+** and **pgvector extension**:

   ```bash
   # On Ubuntu/Debian
   sudo apt install postgresql postgresql-contrib
   sudo apt install postgresql-15-pgvector  # or postgresql-16-pgvector

   # On macOS with Homebrew
   brew install postgresql@15
   brew install pgvector

   # Verify pgvector is available
   psql -c "CREATE EXTENSION IF NOT EXISTS vector;"
   ```

2. **Create the development database**:

   ```bash
   sudo -u postgres psql -c "CREATE DATABASE autonomous_research_agent;"
   sudo -u postgres psql -c "CREATE USER ara_user WITH PASSWORD 'ara_password';"
   sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE autonomous_research_agent TO ara_user;"
   sudo -u postgres psql -d autonomous_research_agent -c "CREATE EXTENSION IF NOT EXISTS vector;"
   sudo -u postgres psql -d autonomous_research_agent -c "GRANT ALL ON SCHEMA public TO ara_user;"
   ```

3. **Update connection string in `src/Api/appsettings.json`**:

   ```json
   {
     "ConnectionStrings": {
       "Postgres": "Host=localhost;Database=autonomous_research_agent;Username=ara_user;Password=ara_password"
     }
   }
   ```

4. **Apply migrations**:

   ```bash
   dotnet ef database update \
     --project src/Infrastructure \
     --startup-project src/Api
   ```

### Restore and run

```bash
dotnet restore
dotnet run --project src/Api
```

Start the local embedding service in a second shell:

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r scripts/requirements-local-ml.txt
python scripts/local_embedding_service.py
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
- `OpenRouter`
- `DocumentProcessing`
- `LocalEmbedding`

`src/Api/appsettings.json` includes development-safe starter values. Replace the signing key, issuer, audience, connection string, and Semantic Scholar API key before production use. For local summaries, set `OPENROUTER_API_KEY`. For local embeddings, keep the Python service running at the configured `LocalEmbedding:BaseUrl`.

## Extension points

### Replace local/runtime AI services

- `ISummarizationService`

Summaries stay on OpenRouter. Embeddings are served locally through `scripts/local_embedding_service.py`, and document OCR falls back to `ocrmypdf` when native PDF extraction is weak or empty.

### Add real background execution

- Keep using the `jobs` table and `IJobService`
- Replace or extend the job runner implementation as needed
- Add hosted services or external worker processes that consume the same durable job records

### Improve search quality

- Replace ILIKE keyword search with PostgreSQL full-text search if needed
- Move semantic ranking from in-memory cosine scoring to pgvector distance queries
- Add document-chunk embeddings if you want OCR text itself to become a first-class semantic search source

### Grow analysis workflows

- Enrich JSONB result schemas with typed helper models in `Application`
- Add new analysis types without breaking existing endpoints
- Introduce scheduled or multi-step analysis pipelines through the job model

## Test Setup

### Prerequisites

- .NET 9 SDK
- PostgreSQL 15+ with pgvector extension (for integration tests)
- Docker (optional, for containerized test database)

### Run unit and integration tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/Infrastructure.Tests/
```

### Test database setup

Integration tests require a PostgreSQL database with pgvector extension:

```bash
# Using Docker (recommended)
docker run -d \
  --name ara-test-db \
  -e POSTGRES_DB=ara_test \
  -e POSTGRES_USER=ara_user \
  -e POSTGRES_PASSWORD=ara_password \
  -p 5432:5432 \
  -v pgvector_data:/var/lib/postgresql/data \
  postgres:15-alpine

# Create pgvector extension
docker exec ara-test-db psql -U ara_user -d ara_test -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

### Test configuration

Tests use `appsettings.Testing.json` in the test projects. Default connection assumes Docker test database:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ara_test;Username=ara_user;Password=ara_password"
  }
}
```

### Writing tests

- Unit tests: Mock external dependencies (HttpClient, embedding service)
- Integration tests: Use `TestDatabaseFixture` for database setup/teardown
- Job runner tests: Use `AutonomousJobRunnerTests` as a reference pattern

### Key test files

| File | Purpose |
|------|---------|
| `tests/Infrastructure.Tests/AutonomousJobRunnerTests.cs` | Job execution logic |
| `tests/Infrastructure.Tests/PaperDocumentProcessingServiceTests.cs` | Document processing |
| `tests/Infrastructure.Tests/EmbeddingIndexingServiceTests.cs` | Embedding service |
| `tests/Infrastructure.Tests/PaperAndSummaryEmbeddingIntegrationTests.cs` | Full embedding pipeline |

## Notes

- The current code is intentionally realistic but lightweight: it is a strong v1 foundation, not a finished production system.
