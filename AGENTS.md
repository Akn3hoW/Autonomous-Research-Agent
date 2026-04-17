# AGENTS.md

## Build and test

```bash
# Full restore + build (warnings are errors: TreatWarningsAsErrors=true)
dotnet restore AutonomousResearchAgent.sln
dotnet build AutonomousResearchAgent.sln --configuration Release --no-restore

# Format verification (CI runs with `|| true` to ignore formatter diffs)
dotnet format --verify-no-changes AutonomousResearchAgent.sln --no-restore

# Run all tests
dotnet test AutonomousResearchAgent.sln --configuration Release --no-build --verbosity normal

# Run single test project
dotnet test tests/Infrastructure.Tests/
```

## Database

EF Core migrations require explicit project flags:

```bash
dotnet ef migrations add <name> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet ef migrations validate --no-build  # run from src/Infrastructure
```

## Architecture

- `src/Api` — ASP.NET Core host; entrypoint `Program.cs`
- `src/Application` — use-case logic, service interfaces
- `src/Domain` — entities, enums (no outward dependencies)
- `src/Infrastructure` — EF Core/Npgsql, PostgreSQL+pgvector, external clients
- `src/Workers` — background job runner; references Application+Infrastructure
- `frontend/` — pre-built static JS (Vite-compiled, source not in this repo)

Dependency order: `Api -> Application -> Domain`, `Infrastructure -> Application + Domain`

## Local development

1. Start PostgreSQL 15+ with `pgvector` extension
2. Apply migrations (see above)
3. Run API: `dotnet run --project src/Api`
4. Run embedding service in a separate shell:
   ```bash
   python -m venv .venv && source .venv/bin/activate
   pip install -r scripts/requirements-local-ml.txt
   python scripts/local_embedding_service.py
   ```
5. OpenAPI at `http://localhost:5000/openapi/v1.json`

Connection string default: `Host=localhost;Port=5432;Database=autonomous_research_agent`

## Docker setup (docker-compose.yml)

Services: `postgres` (pgvector/pgvector:pg17.2), `redis` (caching), `embedding-service` (Python, 2 replicas), `api`. All on `ara-network`.

Uses environment variables for secrets (`POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, etc.).

## External services

- **PostgreSQL/pgvector**: primary store; `vector(768)` column for Snowflake Arctic embeddings
- **Redis**: caching layer (5-min TTL by default)
- **Embedding service**: Python; `Snowflake/snowflake-arctic-embed-m-v1.5` model at port 8001
- **Semantic Scholar**: `ISemanticScholarClient` via `SemanticScholar:ApiKey`
- **OpenRouter**: `ISummarizationService` via `OPENROUTER_API_KEY`

## CI workflow order

`build-backend` → `validate-migrations`, `run-tests`, `build-frontend` (parallel) → `build-docker` → `run-e2e-tests`

Backend must build successfully before tests, frontend, or Docker steps can run.

## Key config

`src/Api/appsettings.json`:
- `ConnectionStrings:Postgres` — env-var-overridable
- `LocalEmbedding:BaseUrl` — default `http://127.0.0.1:8001` (embed service)
- `LocalEmbedding:VectorDimensions` — `768` (Snowflake Arctic)
- `DocumentProcessing:OcrExecutablePath` — requires `ocrmypdf` on PATH

## Testing

Integration tests require PostgreSQL with pgvector. Docker setup:
```bash
docker run -d --name ara-test-db -e POSTGRES_DB=ara_test -e POSTGRES_USER=ara_user -e POSTGRES_PASSWORD=ara_password -p 5432:5432 postgres:15-alpine
docker exec ara-test-db psql -U ara_user -d ara_test -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

Unit tests mock external deps (HttpClient, embedding service). Key reference: `AutonomousJobRunnerTests.cs`.
