# ADR-005: Inbox, outbox, and transaction boundaries

- Status: **Accepted**
- Date: 2026-07-17
- Updated: 2026-07-17 (Prompt 05 inbound implementation)

## Context

Inbound events must be durable before acknowledgement. Outbound provider calls must not lose intent on crash. Holding DB transactions across HTTP is a latency and locking hazard.

## Decision

Persist `InboxMessage` and `OutboxMessage` as infrastructure records with explicit states.  
**Inbound:** insert inbox then acknowledge; process later in a short local transaction without HTTP.  
**Outbound:** insert outbox with stable idempotency key; HTTP outside any open DB transaction; complete outbox after reconcile.

### Implemented inbound transaction boundaries

1. **Receipt TX** — Validate instance/event identity; insert `Pending` inbox; uniqueness on `(ProviderInstanceId, EventId)` → typed `Duplicate` success; commit **before** processing. No provider HTTP.
2. **Claim TX** — Select next eligible (`Pending` / `WaitingForDependency` with due `NextAttemptAt`); set `Processing` and bump `RowVersion` lease; bounded concurrency retries on `DbUpdateConcurrencyException`. SQLite prototype materializes a small candidate batch and filters/orders `DateTimeOffset` in memory (SQLite EF cannot translate those comparisons/ORDER BY).
3. **Apply TX** — Deserialize outside the lock need; stage Vendor/Job upsert + identity link + audit + inbox `Completed` (or dependency/retry/DLQ state) in **one** `SaveChanges`. On failure, clear change tracker so partial staged entities are not committed as completed work.
4. **Poll orchestration** — Provider HTTP (`List` snapshots) runs **outside** DB transactions. Contractors are imported before work orders. Checkpoint (`ConnectorState.LastCheckpoint`) is written only after the receive/process batch for that cycle.

### Synthetic polling event identity

Format: `poll:{instance}:{entityType}:{externalId}:v{version}`  
When contractor `EntityVersion` is absent: `...:h{sha256(payload)}`.

**Limitation:** If the provider omits versions and payload content drifts without a stable key, hash-based ids treat each content change as a new event (correct for replay safety) but cannot detect “same logical version, different serialization.” Prefer provider versions when available. Random event ids are forbidden.

## Alternatives considered

- Process webhook fully before HTTP 2xx — timeout/duplicate risk without durable receipt.
- Dual-write canonical + HTTP without outbox — lost updates on crash.
- Generic repository over `DbSet` — leaks persistence details into use cases.

## Consequences

- Clear crash recovery story; poll and webhook share `Receive` → `Process` → `Apply*`.
- In-process poll gate only; **distributed** scheduling/locking deferred to production.
- Application uses `IConnectorUnitOfWork` / writers rather than `DbSet` surfaces.

### Implemented outbound transaction boundaries (Prompt 07)

1. **Queue TX** — Re-validate Job/Vendor eligibility; insert `OutboxMessage` with stable idempotency key `fieldflow:{instance}:{jobId}:dispatch:v1`; append `dispatch.requested` audit; commit **without** FieldFlow HTTP.
2. **Claim TX** — Select due `Pending` outbox; set `Processing` + `RowVersion` lease (SQLite materializes/filters `DateTimeOffset` in memory, same as inbox).
3. **Provider HTTP** — Create work order with `Idempotency-Key` and Job ID `clientReference` **outside** any DB transaction.
4. **Complete TX** — On confirmed or reconciled success: upsert work-order identity, set Job `dispatched`, complete outbox, audit. On ambiguous create: reconcile by client reference before any repeat POST (same key only).

## Production evolution

Broker-backed outbox relay, competing consumers with row leases, backlog metrics, distributed poll/outbox locks.
