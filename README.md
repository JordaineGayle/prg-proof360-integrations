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
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

## Run

```bash
dotnet run --project src/PRG.FieldFlow.Mock --launch-profile http
dotnet run --project src/PRG.Proof360.Integrations.Api --launch-profile http
```

Health checks:

- Connector: `GET /health/live`, `GET /health/ready`
- Mock: `GET /health`

Configuration placeholders: `.env.example` (never commit real secrets).

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

## Current status

Prompt 03 complete for the FieldFlow mock + assumed contract. Connector FieldFlow mapping, inbound/outbound workers remain deferred (Prompt 04+).
