# Runbook: Dead-letter and replay

## Classification

| Class | Examples | Disposition |
|---|---|---|
| Permanent | Validation, unsupported schema/event, approval rejection | Immediate `DeadLettered` |
| Retryable | Timeout, unavailable, rate limit, concurrency | Bounded `RetryAt` then DLQ |
| Dependency | Missing contractor mapping | `WaitingForDependency` then exhausted → DLQ |
| NeedsAttention | Provider 401/403 | DLQ + connector **NeedsAttention** |

Failure history is append-only JSON on the message (`FailureHistoryJson`) plus audit rows. Replay must not erase history.

## Local admin replay (prototype)

**Endpoint:** `POST /admin/inbox/{inboxMessageId}/replay`

```bash
curl -X POST "http://localhost:5203/admin/inbox/$ID/replay" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ops-replay-1" \
  -d '{"operatorId":"jordaine","reason":"schema mapping fixed"}'
```

**Prototype authorization**

| Environment | `AdminReplay:Enabled` | Gate |
|---|---|---|
| Development | `true` | Allowed when `OperatorToken` empty; if token set, require `X-Admin-Operator-Token` |
| Non-Development | must be `true` | Requires configured `OperatorToken` + header match |
| Disabled | `false` | `404` |

Replay only accepts `DeadLettered` inbox messages. It keeps `EventId` / `PayloadHash` / failure history, sets a new causation id, clears last-error fields, and re-queues as `Pending`. Canonical apply remains idempotent.

## Production authorization (deferred)

- Require authenticated operator identity (SSO/RBAC), not a shared header token.
- Dual-control / approval for high-impact replays (dispatch-related or PII-adjacent payloads).
- Audit operator, reason, before/after state, and ticket reference.
- Rate-limit replay and block replay of non-dead-letter work.
- Prefer outbox replay with the same stable idempotency key after reconcile.

## Immediate actions

1. Inspect `GET /connectors/fieldflow/health` — DLQ depth, oldest backlog age, circuit.
2. Read audit for `message.dead_lettered` / `webhook.verify` / apply outcomes (hashes only).
3. Fix root cause (mapping, credentials, provider outage).
4. Replay with operator + reason; confirm `replay.completed` audit and successful apply.
5. If NeedsAttention (auth), rotate `FieldFlow__ApiKey` before replay storms.
