# ADR-007: Retry, circuit, and ambiguous POST policy

- Status: **Proposed**
- Date: 2026-07-17

## Context

FieldFlow will throttle and fail. Blindly retrying POST can create duplicates. Nested retry loops obscure ownership. An open circuit should degrade the connector, not Proof360 core liveness.

## Decision (proposed)

Classify failures (transient, rate-limited, permanent, auth, ambiguous write). One HTTP retry owner in the provider resilience pipeline (bounded, jittered; honour `Retry-After`). Inbox/outbox have separate bounded business attempts. Circuit opens on sustained transient failure; half-open uses a single probe. For ambiguous POST, reconcile by client reference / identity before any repeat create. Stable `Idempotency-Key` on work-order create.

## Alternatives

- Infinite retries — operational noise and thundering herd.
- Retry POST without reconcile/idempotency — duplicate WorkOrders.
- Fail the whole API when FieldFlow is down — unnecessary blast radius.

## Consequences

- Health states: Healthy / Degraded / Offline / NeedsAttention.
- Tests must demonstrate open, short-circuit, recovery.
- Auth failures surface Needs Attention without aggressive retry.

## Production evolution

Shared resilience config per tenant, alerting on DLQ/circuit, and provider-specific budgets.
