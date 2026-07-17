# FieldFlow Mock Contract (assumed)

Local-only assumed FieldFlow surface for the assignment. The mock (`PRG.FieldFlow.Mock`) and the connector FieldFlow adapter **do not share DTO assemblies**; they communicate with JSON over HTTP so contract drift is detectable at runtime/tests.

**Base URL (local):** `http://localhost:5210`  
**Auth header:** `X-Api-Key: <FieldFlowMock:ApiKey>`  
**IDs:** opaque strings (for example `ctr-1001`, `wo-2001`). Never treat as Proof360 `vendor_id` / `job_id`.  
**Timestamps:** UTC ISO-8601 (`DateTimeOffset`).  
**Deferred (not implemented):** Invoice, PaymentStatus, Location, Appointment endpoints.

Configuration section: `FieldFlowMock` (env: `FieldFlowMock__ApiKey`, `FieldFlowMock__WebhookHmacSecret`, `FieldFlowMock__ProviderInstanceId`).

---

## Status vocabulary

| Status | Meaning |
|---|---|
| `open` | Newly created / open |
| `scheduled` | Scheduled |
| `in_progress` | In progress |
| `done` | Completed |
| `void` | Cancelled / void |

---

## `GET /health`

No API key required.

```json
{
  "status": "Healthy",
  "service": "PRG.FieldFlow.Mock",
  "providerInstanceId": "fieldflow-local-1",
  "utc": "2026-07-17T14:00:00+00:00"
}
```

When test control sets health unavailable → `503` with `"status": "Unavailable"`.

---

## `GET /contractors`

Requires API key.

```json
[
  {
    "contractorId": "ctr-1001",
    "complianceId": "CMP-1001",
    "active": true,
    "displayName": "Northwind Plumbing Fixtures Ltd",
    "license": { "number": "LIC-1001", "expiresOn": "2027-12-31" },
    "insurance": { "policy": "INS-1001", "expiresOn": "2027-06-30", "coverage": "2000000 CAD" },
    "wcbNumber": "WCB-1001"
  }
]
```

Fixture `wo-2099` references `ctr-missing-999`, which is **absent** from this list (dependency-deferral demo).

---

## `GET /work-orders`

Requires API key. Returns all in-memory work orders.

Fixture `wo-2100` includes an additive optional field (`unexpectedOptionalTag`) for schema-evolution tests.

---

## `GET /work-orders/{id}`

Requires API key. `404` with stable error body when missing.

---

## `POST /work-orders`

Requires API key and header **`Idempotency-Key`**.

`clientReference` **must** be the Proof360 Job ID (reconciliation key).

### Request

```http
POST /work-orders
X-Api-Key: <key>
Idempotency-Key: 7f3c2a1e-create-job-100
Content-Type: application/json
```

```json
{
  "clientReference": "100",
  "contractorId": "ctr-1001",
  "customerName": "Ada Fixture",
  "customerPhone": "+1-555-0100",
  "customerEmail": "ada.fixture@example.test",
  "addressStreet": "100 Mock Street",
  "addressUnit": null,
  "addressCity": "Calgary",
  "addressPostal": "T2P1J9",
  "serviceType": "plumbing",
  "subcategory": "leak",
  "windowStart": "2026-08-01T15:00:00Z",
  "windowEnd": "2026-08-01T17:00:00Z",
  "notes": "Create from Proof360"
}
```

### Response (first create) — `201`

```json
{
  "workOrderId": "wo-1001",
  "contractorId": "ctr-1001",
  "clientReference": "100",
  "status": "open",
  "entityVersion": 1,
  "customerName": "Ada Fixture",
  "customerPhone": "+1-555-0100",
  "customerEmail": "ada.fixture@example.test",
  "addressStreet": "100 Mock Street",
  "addressCity": "Calgary",
  "addressPostal": "T2P1J9",
  "serviceType": "plumbing",
  "subcategory": "leak",
  "windowStart": "2026-08-01T15:00:00+00:00",
  "windowEnd": "2026-08-01T17:00:00+00:00",
  "notes": "Create from Proof360"
}
```

### Idempotency behavior

| Case | Result |
|---|---|
| First key + valid body | `201` + store response |
| Same key + equivalent body | `200` + original response |
| Same key + different body | `409` `idempotency_conflict` |
| Missing `Idempotency-Key` | `400` `idempotency_key_required` |

---

## `PATCH /work-orders/{id}/status`

Requires API key.

```json
{
  "status": "in_progress",
  "entityVersion": 3
}
```

If `entityVersion` is omitted, the mock increments the current version by 1. Unknown status → `400`.

---

## Webhook envelope → connector `POST /webhooks/events`

The mock can build/sign (and optionally POST) an envelope. Signing uses the **raw body bytes**.

### Canonical signing string

```text
{unixSeconds}.{rawBody}
```

- HMAC-SHA256 over that UTF-8 prefix + raw body bytes  
- Signature header value: lowercase hex  
- Timestamp header: unix seconds matching the prefix

### Headers

| Header | Purpose |
|---|---|
| `X-FieldFlow-Provider-Instance` | Provider instance id |
| `X-FieldFlow-Event-Id` | Unique event id (duplicates allowed for at-least-once demos) |
| `X-FieldFlow-Event-Type` | e.g. `work_order.status_changed` |
| `X-FieldFlow-Schema-Version` | e.g. `1.0` |
| `X-FieldFlow-Entity-Version` | Entity sequence for ordering |
| `X-FieldFlow-Timestamp` | Unix seconds |
| `X-FieldFlow-Signature` | Hex HMAC-SHA256 |

### Body example

```json
{
  "eventId": "evt-demo-001",
  "eventType": "work_order.status_changed",
  "schemaVersion": "1.0",
  "entityVersion": 2,
  "occurredAt": "2026-07-17T14:05:00+00:00",
  "providerInstanceId": "fieldflow-local-1",
  "data": {
    "workOrderId": "wo-2001",
    "contractorId": "ctr-1001",
    "status": "scheduled",
    "entityVersion": 2,
    "customerName": "Ada Fixture",
    "addressStreet": "100 Mock Street",
    "addressCity": "Calgary",
    "serviceType": "plumbing"
  }
}
```

Out-of-order demos: emit a higher `entityVersion` before a lower one (via `/_test/webhooks/build` overrides).

---

## Local-only test controls (`/_test`)

Not part of the provider contract. No API key.

| Endpoint | Purpose |
|---|---|
| `POST /_test/reset` | Reseed fixtures; clear idempotency, webhooks, failures |
| `POST /_test/failures` | Deterministic 429 / 500 / timeout / health / ambiguous POST |
| `GET /_test/work-orders/by-client-ref/{clientReference}` | Reconcile after ambiguous POST |
| `GET /_test/webhooks/emitted` | Inspect built/sent events |
| `POST /_test/webhooks/build` | Build signed headers + body fixture |
| `POST /_test/webhooks/send` | Demo helper: POST signed event to connector URL |

### Failure injection body

```json
{
  "rateLimitCount": 2,
  "retryAfterSeconds": 1,
  "serverErrorCount": 1,
  "timeoutCount": 1,
  "timeoutDelayMilliseconds": 250,
  "healthUnavailable": true,
  "ambiguousNextPost": true
}
```

Injected 429/500/timeout apply to normal provider routes only (not `/_test`, not `/health`).

---

## Error body shape

```json
{
  "code": "unauthorized",
  "message": "Missing or invalid API key."
}
```

API key values are never logged by the mock.
