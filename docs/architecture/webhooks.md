# Inbound webhooks (Prompt 06)

## Canonical signing string

```text
{unixSeconds}.{rawBody}
```

- HMAC-SHA256 over UTF-8 `{unixSeconds}.` concatenated with the **exact raw body bytes**
- Header: `X-FieldFlow-Signature` (lowercase hex; optional `sha256=` prefix stripped)
- Header: `X-FieldFlow-Timestamp` (unix seconds; must match the signed prefix)

## Replay window

Configured by `FieldFlow:WebhookTimestampSkewSeconds` (default **300** seconds).  
Timestamps outside `|now - timestamp| <= skew` are rejected with `401` (`webhook_timestamp_skew`). No inbox or canonical mutation.

## Endpoint: `POST /webhooks/events`

1. Enforce `FieldFlow:MaxWebhookBodyBytes` (default 64 KiB).
2. Read raw body **once** (never deserialize/reserialize before verify).
3. Verify HMAC + skew + provider instance binding.
4. Normalize supported types through the FieldFlow ACL into a `WorkOrderSnapshot` envelope.
5. Durably insert inbox (`ReceiveProviderEvent`) — **no processing in the endpoint**.
6. Return **`202 Accepted`** with `{ status: "accepted"|"duplicate", inboxMessageId, correlationId }`.

Invalid signatures → `401` Problem Details + sanitized `webhook.verify` audit (correlation id, instance if safe, body hash, failure code). Never log raw body, signature, or secret.

## Acknowledgement and duplicates

| Outcome | HTTP | Meaning |
|---|---|---|
| New event | 202 `accepted` | Inbox `Pending` inserted |
| Same `(providerInstanceId, eventId)` | 202 `duplicate` | Idempotent success; no second inbox row |

Duplicates and stale versions are **not** HTTP 500.

## Ordering

- Prefer `entityVersion` / `X-FieldFlow-Entity-Version` over wall-clock `occurredAt`.
- `incoming < last_applied` → audited `ignored_stale` (no mutation).
- `incoming == last_applied` and payload hash differs → audited `version_payload_conflict` (security anomaly, no mutation).
- Newer versions still pass `JobStatusTransitionPolicy`.
- If version is missing/untrusted (`<= 0`), treat webhook as notification and **reconcile via GET** work order (consistency tradeoff: apply current provider state, not the stale envelope).

## Unsupported events

Unknown event type or schema version is **stored** then **dead-lettered** on process (`unsupported_event_type`) for later inspection/replay after support is added.

## Demo commands (placeholder secrets)

```bash
# Build a signed fixture via the mock (no connector secret needed on this call)
curl -s -X POST http://localhost:5210/_test/webhooks/build \
  -H 'Content-Type: application/json' \
  -d '{"workOrderId":"wo-2001","status":"scheduled","entityVersion":2}'

# Send signed event to the connector (mock signs with FieldFlowMock__WebhookHmacSecret)
curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"scheduled","entityVersion":2}'
```

Ensure `FieldFlow__WebhookHmacSecret` on the API matches `FieldFlowMock__WebhookHmacSecret` on the mock.

Sequence diagram: `docs/architecture/inbound-sequence.mmd`.
