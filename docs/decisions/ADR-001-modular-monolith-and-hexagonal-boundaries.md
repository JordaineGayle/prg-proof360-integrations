# ADR-001: Modular monolith and hexagonal boundaries

- Status: **Accepted**
- Date: 2026-07-17

## Context

The assignment needs a small, explainable connector prototype with clear separation between Proof360 canonical semantics, provider adapters, and infrastructure. Microservices would add operational noise without proving the integration invariants.

## Decision

Ship one connector deployable (`PRG.Proof360.Integrations.Api`) plus one FieldFlow mock deployable (`PRG.FieldFlow.Mock`). Structure the connector as a modular monolith with ports-and-adapters:

| Project | Responsibility |
|---|---|
| Domain | Canonical representations and pure policies only |
| Core | Provider-neutral connector contracts and integration metadata |
| Application | Use cases, orchestration, transaction boundaries |
| FieldFlow | Provider DTOs, mapping, HTTP, webhook verification |
| Infrastructure | EF, workers, clocks, health mechanics |
| API | Endpoints and composition root |

Dependency rules are enforced by project references and architecture tests.

## Alternatives considered

- Microservice-per-entity or per-pattern — rejected for prototype complexity.
- Single anemic project — harder to protect canonical boundaries.
- Shared DTO assembly between mock and connector — rejected; mock shares no connector production projects.

## Consequences

- Clear review story and replaceable FieldFlow adapter.
- Requires ongoing architecture tests so layers do not leak.
- In-process workers are acceptable later; production may extract hosts without changing ports.

## Production evolution

Extract workers behind the same Application ports; keep adapter isolation; deploy the mock only in non-production environments.
