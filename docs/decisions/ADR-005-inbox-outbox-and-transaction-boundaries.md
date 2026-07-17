# ADR-005: Inbox, outbox, and transaction boundaries

- Status: **Accepted**
- Date: 2026-07-17

## Context

Inbound events must be durable before acknowledgement. Outbound provider calls must not lose intent on crash. Holding DB transactions across HTTP is a latency and locking hazard.

## Decision

Persist `InboxMessage` and `OutboxMessage` as infrastructure records with explicit states.  
**Inbound:** insert inbox then acknowledge; process later in a short local transaction without HTTP.  
**Outbound:** insert outbox with stable idempotency key; HTTP outside any open DB transaction; complete outbox after reconcile.

## Alternatives considered

- Process webhook fully before HTTP 2xx — timeout/duplicate risk without durable receipt.
- Dual-write canonical + HTTP without outbox — lost updates on crash.
- Generic repository over `DbSet` — leaks persistence details into use cases.

## Consequences

- Clear crash recovery story.
- Requires workers/claim semantics in later prompts.
- Application uses `IConnectorUnitOfWork` / writers rather than `DbSet` surfaces.

## Production evolution

Broker-backed outbox relay, competing consumers with row leases, backlog metrics.
