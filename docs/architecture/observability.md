# Observability and security (audit / metrics / alerts)

## Correlation and causation

- Header: `X-Correlation-Id`
- Validation: 1–128 chars of `[A-Za-z0-9_.:-]`; invalid/oversized values are replaced with a generated id
- Causation ids are generated at webhook accept, dead-letter, replay, and sync boundaries
- Correlation is preserved across inbox/outbox retries of the same logical operation

## Structured audit

Append-only `audit_events` with sanitized fields only (hashes, categories, ids — never secrets, signatures, raw bodies, phone/email).

Coverage includes sync requested/completed/failed, webhook verify/accept/duplicate/reject/stale, apply outcomes, dispatch request/complete, dependency waiting, dead-letter, replay, and circuit transitions (metrics + health; circuit also increments BCL counters).

## Metrics and activities

BCL `Meter` / `ActivitySource`: `PRG.Proof360.Integrations`

Low-cardinality labels only (`operation`, `outcome`, `entity`, `channel`, `state`).  
**Never** label with customer IDs, external IDs, correlation IDs, phone/email, or raw errors.

Key instruments: provider requests/duration, HTTP retries/rate-limits, circuit transitions, webhooks, inbox/outbox processed, dead-lettered, unresolved dependencies, sync cycles.

## Proposed alerts (rules only — no alert backend in Phase 1)

| Alert | Severity | Threshold assumption | Owner | Immediate action | False-positive notes |
|---|---|---|---|---|---|
| Circuit open / Offline | Sev-1 | Circuit `Open` ≥ 2 min or health Offline | Connector on-call | Check FieldFlow `/health`; pause outbound load; watch half-open | Provider maintenance windows |
| Auth NeedsAttention | Sev-1 | Health `NeedsAttention` or auth audit spike | Connector on-call | Rotate API key/config; do not replay until fixed | Clock skew misclassified as auth rare |
| Dead-letter depth > 0 | Sev-2 | `DeadLetterCount > 0` for 15 min | Connector eng | Triage audit + runbook replay | Single poison message during deploy |
| Oldest backlog age | Sev-2 | Oldest inbox/outbox age > 30 min | Connector eng | Scale workers / inspect dependency waits | Quiet hours with low volume |
| Sync freshness breach | Sev-2 | No successful sync > `OfflineNoSuccessSeconds` | Connector eng | Manual `/sync/*`; check provider | Polling disabled intentionally |
| Elevated 429 / transient ratio | Sev-3 | Rate-limit or 5xx ratio > 20% over 10 min | Connector eng | Back off; verify circuit/retry budget | Load tests |
| Webhook signature reject spike | Sev-2 | Rejected webhooks > 10 / 5 min | Security + connector | Verify HMAC secret/clock; check attacker probing | Client retry storms after secret rotate |

## Security notes

- Health, audit, and logs are sanitized for secrets/PII (see unit/integration markers tests)
- Admin replay is local/prototype-gated — production authz deferred (runbook)
- Metric cardinality is guarded by `ConnectorTelemetry.SanitizeLabel`
