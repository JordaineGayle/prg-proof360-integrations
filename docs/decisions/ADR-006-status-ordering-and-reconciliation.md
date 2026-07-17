# ADR-006: Status ordering and reconciliation

- Status: **Proposed**
- Date: 2026-07-17

## Context

Provider events can arrive late or out of order. Wall-clock timestamps alone are unsafe. Missing versions still need a deterministic policy.

## Decision (proposed)

Prefer provider sequence/version; reject ≤ last applied version as audited no-op. Validate transitions independently. If version is untrusted/absent, treat webhook as notification and fetch current WorkOrder. Terminal states (`completed`, `cancelled`) do not reopen in Phase 1. Assumed FieldFlow→Job map documented in `assumptions.md`.

## Alternatives

- Last-write-wins by timestamp — allows regressions.
- Blind apply every event — non-monotonic operational state.
- Drop events without version — silent data loss; prefer reconcile.

## Consequences

- Stale events are safe no-ops, not endless retries.
- Requires identity link to store `last_applied_version`.
- Status vocabulary is an assumption until PRG confirms.

## Production evolution

Product-signed transition matrix; optional explicit reopen workflow with audit and authorization.
