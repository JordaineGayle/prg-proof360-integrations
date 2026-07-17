# Outbound dispatch (Prompt 07)

## Eligibility (no provider HTTP)

A Job may be queued only when all of the following hold:

| Gate | Rule |
|---|---|
| Status | `qualified` |
| Required fields | `customer_name`, `address_street`, `address_city`, `service_type` |
| Window/scope | `window_start`/`window_end` **or** `notes_scope` |
| Vendor | `assigned_vendor_id` resolves; status is Proof360 `approved`; not `restricted` |
| Identity | No existing FieldFlow work-order identity for the Job |
| Outbox | No conflicting pending/completed outbox for the stable key |

Provider `active` alone never auto-approves a Vendor for dispatch.

## Idempotency key

```text
fieldflow:{providerInstance}:{jobId}:dispatch:v1
```

Never regenerate for the same logical dispatch. Unique in DB on `(provider_instance_id, idempotency_key)`.  
Same key with a different logical payload hash → `idempotency_key_conflict` (409).

## Outbox transaction

In one local TX (no FieldFlow call):

1. Re-validate Job/Vendor  
2. Insert `OutboxMessage` (`Pending`) with serialized `DispatchWorkOrderCommand` + payload hash  
3. Append `dispatch.requested` audit  
4. Commit  

## Worker state machine

| State | Meaning |
|---|---|
| Pending | Eligible to claim when `NextAttemptAt` is due |
| Processing | Claimed via `RowVersion` lease |
| Completed | Identity linked; Job `dispatched`; `ResultReference` = external work-order id |
| DeadLettered | Permanent/auth/validation failure — Needs Attention |

HTTP runs **outside** any open DB transaction. The FieldFlow resilience pipeline owns nested HTTP retries; this worker only schedules bounded outbox attempts (see `docs/architecture/resilience.md`).

## Ambiguous POST

If create times out / response is lost (`AmbiguousWrite`):

1. Do **not** treat as failed-to-create  
2. Reconcile by Proof360 Job ID `clientReference`  
3. If found → complete locally (identity + `dispatched`)  
4. If not found → retry later with the **same** idempotency key  

On confirmed HTTP success followed by local TX failure, stash `ResultReference` and re-queue so the next attempt completes without a second POST.

## Endpoints

| Route | Purpose |
|---|---|
| `POST /connectors/fieldflow/jobs/{jobId}/dispatch` | Queue outbox (Application only) |
| `POST /_demo/seed-qualified-dispatch` | **Development only** — seed approved Vendor + qualified Job |

## Demo

```bash
# Development API + mock
curl -X POST http://localhost:5203/_demo/seed-qualified-dispatch
curl -X POST http://localhost:5203/connectors/fieldflow/jobs/22222222-2222-2222-2222-222222222222/dispatch

# Optional: enable worker
# OutboundDispatch__WorkerEnabled=true
```

Sequence: `docs/architecture/outbound-sequence.mmd`.
