# ADR-007: Retry, circuit, and ambiguous POST policy

- Status: **Accepted**
- Date: 2026-07-17
- Updated: 2026-07-17 (Prompt 08 HTTP resilience + connector health)

## Context

FieldFlow will throttle and fail. Blindly retrying POST can create duplicates. Nested retry loops obscure ownership. An open circuit should degrade the connector, not Proof360 core liveness.

## Decision

### 1. Failure classification

Adapter → `ProviderFailure` → `IntegrationFailure` → `FailureDispositionPolicy`.

| Kind | HTTP retry | Circuit | Disposition |
|---|---|---|---|
| Transient 408/5xx, transport, timeout | Yes (bounded) | Counts | RetryAt / DeadLetter when exhausted |
| 429 rate limit | Yes; honour `Retry-After` (capped) | Counts (capacity pressure) | RetryAt |
| Validation 4xx (400/422) | No | No | DeadLetter |
| Auth 401/403 | No | No | NeedsAttention |
| Ambiguous write | No HTTP retry of “create succeeded?” | No | Reconcile then same-key retry |
| Circuit open | N/A (fail fast) | Open | RetryAt / Unavailable |

### 2. One HTTP retry owner

`Microsoft.Extensions.Http.Resilience` pipeline on the typed `FieldFlowClient` owns per-call retries, attempt timeout, concurrency limiter, and circuit breaker.

Inbox/outbox workers **must not** nest another HTTP retry loop. They only schedule bounded **business** attempts (`OutboundDispatch:MaxAttempts`, inbox attempt budget).

**Policy order (outer → inner):** concurrency limiter → retry → circuit breaker → per-attempt timeout → transport.

### 3. Retry budget

- Per HTTP call: `1 + FieldFlow:Resilience:MaxRetryAttempts` (default **4**).
- Worst-case provider calls for one outbox dispatch operation: `OutboundDispatch:MaxAttempts × (1 + MaxRetryAttempts)` (default **8 × 4 = 32**).
- Caller cancellation is never retried.

### 4. Ambiguous POST

Mark attempt ambiguous, not failed-to-create. Reconcile by Proof360 `clientReference` (Job ID) / identity before any repeat create. Repeat POST only with the same stable `Idempotency-Key` when reconcile confirms absence.

Stable key: `fieldflow:{providerInstance}:{jobId}:dispatch:v1`.

### 5. Circuit

- Opens on failure ratio within sampling window after minimum throughput (availability failures only).
- Prolonged **429 counts toward the circuit**: sustained capacity pressure is provider unavailability for dispatch/sync.
- While open: fail fast, no HTTP.
- Half-open: one controlled probe; success closes and updates freshness; failure reopens.

### 6. Health

| Status | Rule (precedence) |
|---|---|
| NeedsAttention | Auth/config failure flag |
| Offline | Circuit Open, or prolonged silence after failure |
| Degraded | HalfOpen, backlog/DLQ thresholds, rate-limit trend |
| Healthy | Otherwise |

- `/health/live`: process only — FieldFlow outage must not fail it.
- `/health/ready`: local SQLite — FieldFlow outage must not fail it (work can still queue).
- `/connectors/fieldflow/health`: sanitized connector projection.

## Alternatives considered

- Infinite retries — operational noise and thundering herd.
- Retry POST without reconcile/idempotency — duplicate WorkOrders.
- Fail API liveness when FieldFlow is down — unnecessary blast radius.
- Nested retry in workers + client — opaque attempt multiplication.

## Consequences

- Configuration under `FieldFlow:Resilience` and `ConnectorHealth` (see `docs/architecture/resilience.md`).
- Outbox `ResultReference` stash recovers local completion after successful/ambiguous create without a second POST.
- Tests in `ResilienceTests` cover retry bounds, Retry-After, circuit transitions, cancellation, and health sanitization.

## Production evolution

Shared resilience config per tenant, alerting on DLQ/circuit, and provider-specific budgets.
