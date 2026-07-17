# ADR-002: Sidecar integration metadata is not canonical

- Status: **Accepted**
- Date: 2026-07-17

## Context

Proof360 allows only `Vendor`, `Job`, and `Transcript` as canonical entities, yet connectors need durable identity links, inbox/outbox, audit, and health state.

## Decision

Store integration metadata as connector infrastructure records in `PRG.Proof360.Integrations.Core.Integration`, persisted beside but outside canonical tables. Never place FieldFlow IDs into `vendor_id` / `job_id`. Never use `ai_json`, `notes_scope`, or `rationale` as payload dumps.

## Alternatives considered

- Embed provider IDs in canonical fields — violates assignment.
- External mapping microservice — unnecessary for Phase 1.
- Store raw provider JSON on Job — breaks schema protection and privacy.

## Consequences

- Lineage and dedupe live in `provider_identity_links` and inbox uniqueness.
- Domain stays free of EF/HTTP/FieldFlow types (enforced by architecture tests).
- Reviewers can verify the three-entity rule without denying operational tables.

## Production evolution

Sidecar tables may move to a dedicated integration schema/database; contracts with Proof360 remain ID-based.
