# Persistence design

## Tables

| Table | Purpose |
|---|---|
| `vendors` / `jobs` / `transcripts` | Canonical Proof360 entities only; exact assignment column names |
| `provider_identity_links` | Sidecar external↔canonical lineage |
| `inbox_messages` | Durable inbound receipt, deferral, DLQ state |
| `outbox_messages` | Durable outbound intent + idempotency |
| `audit_events` | Append-only sanitized decisions |
| `connector_states` | Health/checkpoint projection |

## Unique constraints (correctness)

| Constraint | Why database-enforced |
|---|---|
| `(provider_instance_id, external_entity_type, external_id)` | Concurrent polling/webhook races cannot create two links |
| `(provider_instance_id, canonical_entity_type, canonical_id)` | One active external mapping per canonical row per instance |
| `(provider_instance_id, event_id)` on inbox | Duplicate deliveries collapse before mutation |
| `(provider_instance_id, idempotency_key)` on outbox | Repeated dispatch intents stay singular |

Pre-checks are helpful for UX; they are not the source of truth under concurrency.

## Concurrency

`RowVersion` concurrency tokens on identity, inbox, and outbox support optimistic claim/update under SQLite. Production PostgreSQL can keep the same tokens or adopt `xmin`/row versions.

## Transactions (intent)

- Inbound ack: insert inbox, commit, then return 202 (implemented in later prompts).
- Inbound process: claim inbox + upsert canonical + identity + audit + complete inbox in one local transaction; never across HTTP.
- Outbound: insert outbox with qualification decision; HTTP outside the transaction; complete outbox after reconcile.

## Development initialization

`ConnectorPersistence:ApplyMigrationsOnStartup=true` in Development runs `MigrateAsync` via a hosted service. **Limitation:** production should apply migrations as a controlled release step, not as a side effect of every process start.

## Retention caveat

Inbox `PayloadEnvelope` may contain sensitive customer fields. Prototype retains envelopes for replay; production needs retention, encryption, and access controls.

## Migration command

```bash
dotnet ef migrations add <Name> \
  --project src/PRG.Proof360.Integrations.Infrastructure \
  --startup-project src/PRG.Proof360.Integrations.Infrastructure \
  --output-dir Persistence/Migrations
```
