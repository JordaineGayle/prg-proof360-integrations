# FieldFlow HTTP resilience and connector health

## Single retry owner

Per-HTTP-call retries live only in the FieldFlow resilience pipeline (`FieldFlowResiliencePipeline`). Inbox/outbox workers schedule durable business attempts; they do not loop HTTP.

## Pipeline order

1. Concurrency limiter (bulkhead)
2. Bounded exponential retry (+ jitter; `Retry-After` for 429)
3. Circuit breaker
4. Per-attempt timeout
5. Transport (attempt counter → HTTP)

## Configuration keys (safe examples)

| Key | Example | Notes |
|---|---|---|
| `FieldFlow:Resilience:AttemptTimeoutMilliseconds` | `3000` | Per attempt |
| `FieldFlow:Resilience:MaxRetryAttempts` | `3` | Retries after first try → 4 attempts |
| `FieldFlow:Resilience:BaseDelayMilliseconds` | `200` | Exponential base |
| `FieldFlow:Resilience:MaxDelayMilliseconds` | `5000` | Cap for exponential backoff |
| `FieldFlow:Resilience:MaxRetryAfterMilliseconds` | `30000` | Cap for provider `Retry-After` |
| `FieldFlow:Resilience:DisableRetryDelays` | `false` | Tests only → force zero delay |
| `FieldFlow:Resilience:CircuitFailureRatio` | `0.5` | 50% failures in window |
| `FieldFlow:Resilience:CircuitMinimumThroughput` | `5` | Samples before open |
| `FieldFlow:Resilience:CircuitSamplingDurationSeconds` | `30` | Window |
| `FieldFlow:Resilience:CircuitBreakDurationSeconds` | `15` | Open → half-open |
| `FieldFlow:Resilience:ConcurrencyLimit` | `64` | Bulkhead permits |
| `ConnectorHealth:DegradedInboxBacklogThreshold` | `50` | Degraded if at/above |
| `ConnectorHealth:OfflineNoSuccessSeconds` | `300` | Offline silence after failure |

Never put API keys or HMAC secrets in health responses or logs.

## Worst-case attempt count

- One resilience-wrapped call: `1 + MaxRetryAttempts` (default **4**).
- One outbox dispatch business operation: `OutboundDispatch:MaxAttempts × (1 + MaxRetryAttempts)` (default **32**).

## Failure-mode matrix

| Trigger | HTTP retry | User-visible state | Audit / disposition | Alert | Recovery |
|---|---|---|---|---|---|
| Transient 5xx / 408 / timeout | Bounded + jitter | Degraded if sustained; Offline if circuit opens | RetryAt → DLQ | Circuit open / DLQ growth | Provider recovers; half-open probe |
| 429 + `Retry-After` | Honour header (capped) | Degraded on trend | RetryAt | Rate-limit trend | Budget clears; backoff |
| Validation 400/422 | None | Unchanged (message DLQ) | DeadLetter | DLQ | Fix payload / mapping |
| 401/403 | None | **NeedsAttention** | NeedsAttention | Auth failure | Rotate credentials / config |
| Circuit open | Fail fast | **Offline** | Unavailable / RetryAt | Circuit open | Break duration → probe |
| Ambiguous POST | No blind retry | Degraded/Offline if transport storm | Reconcile + same key | Ambiguous write | Identity/reconcile completes |
| Caller cancel | None | N/A | None | None | Caller retry later |

## Provider-outage runbook

1. Check `GET /health/live` (must stay OK) and `GET /connectors/fieldflow/health`.
2. If `Status=Offline` and `CircuitState=Open`, pause non-critical outbound load; confirm FieldFlow mock/provider `/health`.
3. If `NeedsAttention`, verify `FieldFlow__ApiKey` / base URL (do not log secrets).
4. Inspect inbox/outbox DLQ counts on connector health; redrive only after provider recovery.
5. After provider recovers, wait for half-open probe or restart process to reset in-memory circuit (prototype).
6. Confirm a successful sync/dispatch updates `LastSuccessfulProviderCallAt` / sync checkpoint.

## Endpoints

| Path | Semantics |
|---|---|
| `GET /health/live` | Process alive; ignores FieldFlow |
| `GET /health/ready` | SQLite ready; ignores FieldFlow |
| `GET /connectors/fieldflow/health` | Sanitized connector projection |
