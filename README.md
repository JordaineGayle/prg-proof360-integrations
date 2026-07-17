# PRG.Proof360.Integrations

Field-service connector prototype for the PRG Practical Integration Assignment (Jordaine Gayle).

**Runtime target:** .NET **10** (approved deviation from the kit’s .NET 8 prompt; only SDK 10 is available in this environment).

## Prerequisites

- .NET SDK 10.0.100 or later (`global.json` uses `rollForward: latestFeature`)
- No Docker, PostgreSQL, or live FieldFlow credentials required for the scaffold

## Solution layout

```text
src/
  PRG.Proof360.Integrations.Domain
  PRG.Proof360.Integrations.Core
  PRG.Proof360.Integrations.Application
  PRG.Proof360.Integrations.FieldFlow
  PRG.Proof360.Integrations.Infrastructure
  PRG.Proof360.Integrations.Api          # http://localhost:5203
  PRG.FieldFlow.Mock                    # http://localhost:5210
tests/
  UnitTests / IntegrationTests / ResilienceTests / ArchitectureTests
docs/
  assignment/  decisions/  packages/
```

## Build and test

```bash
dotnet restore
dotnet format
dotnet format --verify-no-changes
dotnet build --configuration Release --no-restore
mkdir -p artifacts/test-results
dotnet test --configuration Release --no-build \
  --results-directory artifacts/test-results \
  --logger "trx;LogFilePrefix=prg-tests"
```

TRX files land under `artifacts/test-results/` (gitignored; one per test project). Requirements mapping: `docs/assignment/requirements-traceability.md`.

## Run

```bash
dotnet run --project src/PRG.FieldFlow.Mock --launch-profile http
dotnet run --project src/PRG.Proof360.Integrations.Api --launch-profile http
```

Health checks:

- Process: `GET /health/live` (ignores FieldFlow), `GET /health/ready` (SQLite only)
- Connector: `GET /connectors/fieldflow/health` (Healthy / Degraded / Offline / NeedsAttention)
- Mock: `GET /health`

Admin replay (Development / `AdminReplay:Enabled`):

```bash
curl -X POST "http://localhost:5203/admin/inbox/$INBOX_ID/replay" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ops-1" \
  -d '{"operatorId":"jordaine","reason":"mapping fixed"}'
```

Observability: `docs/architecture/observability.md`, runbook `docs/runbooks/replay-and-dead-letter.md`, ADR-007. Configuration placeholders: `.env.example` (never commit real secrets).

## FieldFlow mock (Prompt 03)

Assumed local provider at `http://localhost:5210`. Contract: `docs/architecture/fieldflow-mock-contract.md`.

The mock intentionally **does not share DTO assemblies** with `PRG.Proof360.Integrations.FieldFlow`. JSON over HTTP is the contract boundary so fixture drift surfaces in tests instead of compile-time coupling.

### Configuration

| Setting | Env / config |
|---|---|
| API key (`X-Api-Key`) | `FieldFlowMock__ApiKey` |
| Webhook HMAC secret | `FieldFlowMock__WebhookHmacSecret` |
| Provider instance id | `FieldFlowMock__ProviderInstanceId` |

`launchSettings.json` uses `replace-me` placeholders. Override via environment or user-secrets before demos.

### Useful local calls

```bash
# Reset fixtures / failure injection
curl -X POST http://localhost:5210/_test/reset

# Inject two 429s then one 500
curl -X POST http://localhost:5210/_test/failures \
  -H 'Content-Type: application/json' \
  -d '{"rateLimitCount":2,"retryAfterSeconds":1,"serverErrorCount":1}'

# Create work order (Proof360 Job ID = clientReference)
curl -X POST http://localhost:5210/work-orders \
  -H "X-Api-Key: $FieldFlowMock__ApiKey" \
  -H 'Idempotency-Key: demo-1' \
  -H 'Content-Type: application/json' \
  -d '{"clientReference":"100","customerName":"Ada","addressStreet":"1 St","addressCity":"Calgary","serviceType":"plumbing"}'

# Reconcile after ambiguous POST
curl http://localhost:5210/_test/work-orders/by-client-ref/100
```

Invoice / PaymentStatus / Location / Appointment endpoints are deferred (noted only).

## Database (Prompt 02)

SQLite via EF Core. Development can apply migrations on startup:

```json
"ConnectorPersistence": {
  "ConnectionString": "Data Source=connector.dev.db",
  "ApplyMigrationsOnStartup": true
}
```

Create/update migrations:

```bash
dotnet ef migrations add <Name> \
  --project src/PRG.Proof360.Integrations.Infrastructure \
  --startup-project src/PRG.Proof360.Integrations.Infrastructure \
  --output-dir Persistence/Migrations
```

See `docs/architecture/persistence.md`.

## Mapping and source of truth (Prompt 04)

- Capability ports: `docs/architecture/schema-mapping.md`
- Field ownership / status tables: `docs/architecture/source-of-truth.md`
- FieldFlow adapter DTOs are **not** shared with Application/Domain or the mock project

## Typed errors (critical)

- `Result<TSuccess, TFailure>` + `IntegrationFailure` / `ProviderFailure` / disposition policy: `docs/architecture/error-handling.md`
- Cursor rule: `.cursor/rules/error-handling.mdc`
- API: RFC 7807 via `ProblemDetailsMapper`; unexpected exceptions sanitized once at the outer boundary

## Inbound sync (Prompt 05)

Durable inbox + shared apply path for poll and webhooks. Sequence: `docs/architecture/inbound-sequence.mmd`. ADR: `docs/decisions/ADR-005-inbox-outbox-and-transaction-boundaries.md`.

| Setting | Env / config |
|---|---|
| Enable background poller | `InboundSync__PollingEnabled` (default `false`) |
| Poll interval (seconds) | `InboundSync__PollingIntervalSeconds` |
| Max process batch | `InboundSync__MaxProcessBatch` |

Manual sync (Application handlers only; no EF in endpoints):

```bash
# Requires Mock + API running and FieldFlow__BaseUrl pointing at the mock
curl -X POST http://localhost:5203/sync/contractors
curl -X POST http://localhost:5203/sync/work-orders
```

Recommended demo order: sync contractors first, then work orders (worker does the same). Background polling stays off unless you set `InboundSync__PollingEnabled=true`.

## Webhooks (Prompt 06)

`POST /webhooks/events` — verify HMAC over `{unixSeconds}.{rawBody}`, durable inbox receipt, `202` for accept/duplicate. Docs: `docs/architecture/webhooks.md`. Ordering ADR: `docs/decisions/ADR-006-status-ordering-and-reconciliation.md`.

```bash
# Mock helper signs with FieldFlowMock__WebhookHmacSecret (must match FieldFlow__WebhookHmacSecret)
curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"scheduled","entityVersion":2}'
```

Replay window: `FieldFlow__WebhookTimestampSkewSeconds` (default 300).

## Outbound dispatch (Prompt 07)

Transactional outbox for qualified Jobs. Docs: `docs/architecture/outbound-dispatch.md`, sequence `docs/architecture/outbound-sequence.mmd`.

| Setting | Env / config |
|---|---|
| Enable outbox worker | `OutboundDispatch__WorkerEnabled` (default `false`; `true` in Development) |
| Worker interval | `OutboundDispatch__WorkerIntervalSeconds` |
| Max outbox attempts | `OutboundDispatch__MaxAttempts` |

```bash
# Development-only seed (approved Vendor + qualified Job)
curl -X POST http://localhost:5203/_demo/seed-qualified-dispatch

# Queue dispatch (no FieldFlow call in the request TX)
curl -X POST http://localhost:5203/connectors/fieldflow/jobs/22222222-2222-2222-2222-222222222222/dispatch
```

Idempotency key: `fieldflow:{instance}:{jobId}:dispatch:v1`. Ambiguous POST reconciles by Job ID client reference before any retry (same key only).

## Current status

Prompts 05–10 landed: inbound/outbound, resilience/health, audit/replay/observability, full suite hardening, and filled requirements traceability. Architecture/Leadership PDFs and ZIP packaging remain Prompt 11–12.
