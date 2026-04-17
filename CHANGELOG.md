# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-04-17

### Added
- Initial stable v1 release
- Papers CRUD and import via Semantic Scholar
- Paper summarization via OpenRouter
- Semantic and hybrid search with pgvector embeddings
- Document OCR with ocrmypdf integration
- Background job system with durable records
- Workers project for background job processing
- Analysis endpoints for paper comparison and insights
- SignalR hub for job status real-time updates
- JWT authentication with role-based authorization
- OpenAPI documentation
- Docker Compose setup with postgres, redis, and embedding service
- Full test suite with unit and integration tests

### Architecture
- Modular monolith structure: Api, Application, Domain, Infrastructure, Workers
- Stable DTO-based API contracts
- Hybrid structured + flexible JSONB storage
- PostgreSQL/pgvector for vector similarity search

## [0.1.0] - 2026-03-12

### Added
- Initial project structure
- Database migrations baseline
