# ADR-004: SQLite prototype, PostgreSQL production

- Status: **Accepted**
- Date: 2026-07-17

## Context

The submission must run locally without PRG infrastructure. Persistence still needs real uniqueness, transactions, and migrations to prove reliability design. EF Core packages are not added in the scaffold prompt; this ADR locks the persistence direction before Prompt 02.

## Decision

Use SQLite (via EF Core) for the prototype when persistence is introduced. Design schema and transaction boundaries so they port to PostgreSQL. Document SQLite limitations (file locking, concurrency differences).

## Alternatives considered

- PostgreSQL-only — heavier local prerequisite; may block reviewers.
- In-memory store only — insufficient for durable inbox/outbox and crash-safety proof.
- Provider database coupling — violates external-contract architecture.

## Consequences

- Simple local `dotnet run` story after Prompt 02.
- Some concurrency tests will be SQLite-aware; production needs PostgreSQL isolation validation.
- Migrations should avoid SQLite-only dead ends where practical.

## Production evolution

PostgreSQL (or managed equivalent), connection pooling, backup/PITR, and optionally a dedicated integration database.
