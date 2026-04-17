# Contributing to Autonomous Research Agent

Thank you for your interest in contributing!

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Create a feature branch (`git checkout -b feature/my-feature`)

## Development Setup

### Prerequisites
- .NET 9 SDK
- PostgreSQL 15+ with pgvector extension
- Python 3.11+ (for embedding service)
- ocrmypdf (for document OCR)

### Initial Setup

```bash
# Install dependencies
dotnet restore AutonomousResearchAgent.sln

# Build
dotnet build AutonomousResearchAgent.sln --configuration Release --no-restore

# Run tests
dotnet test AutonomousResearchAgent.sln --configuration Release --no-build --verbosity normal
```

## Coding Standards

- Follow existing code style and conventions
- Run `dotnet format` before committing
- Add unit tests for new functionality
- Update documentation for user-facing changes

## Making Changes

1. Make your changes on your feature branch
2. Run tests: `dotnet test`
3. Run format check: `dotnet format --verify-no-changes AutonomousResearchAgent.sln --no-restore`
4. Commit with a clear message
5. Push to your fork
6. Open a Pull Request

## Project Structure

- `src/Api` - ASP.NET Core Web API
- `src/Application` - Use-case logic and service interfaces
- `src/Domain` - Core entities and enums
- `src/Infrastructure` - EF Core, external clients, background jobs
- `src/Workers` - Background job runner process
- `tests/` - Unit and integration tests

## Commit Message Format

```
type(scope): description

Types: feat, fix, docs, refactor, test, chore
```

## Questions?

Open an issue for discussion before making large changes.
