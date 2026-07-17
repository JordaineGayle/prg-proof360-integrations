# PRG Practical Integration Assignment — Prototype README

**Candidate:** Jordaine Gayle  
**Artifact:** `03_README.md`  
**Solution:** `PRG.Proof360.Integrations`  
**Runtime:** .NET **10** (approved deviation from kit .NET 8; only SDK 10 available in the build environment)

---

## Purpose and demonstrated behaviors

Local FieldFlow connector prototype that demonstrates:

- Contractor → Vendor and WorkOrder → Job mapping with origin-dependent source of truth  
- Polling and webhook intake converging on one durable inbox apply path  
- HMAC webhook verification; duplicate and concurrent-idempotent effects  
- Entity-version ordering; unknown-contractor dependency wait → resolve / exhaust → DLQ → replay  
- Qualified Job outbound dispatch via transactional outbox + stable Idempotency-Key  
- Ambiguous POST reconciliation before retry  
- Bounded HTTP retry, Retry-After, circuit breaker, connector health  
- Structured audit + correlation; sanitized logs/health  
- Automated unit, integration, resilience, and architecture tests  

Delivery model: **at-least-once + idempotent effects** (never exactly-once).

---

## Technology choices

| Choice | Reason |
|---|---|
| .NET 10 / ASP.NET Core | Available SDK; typed HTTP + DI + health |
| Modular monolith + ports | Clear boundaries without microservice ops cost |
| EF Core + SQLite | Zero-infra local persistence; portable to PostgreSQL |
| Microsoft.Extensions.Http.Resilience | Bounded retry/circuit without custom Polly sprawl |
| Separate FieldFlow mock host | Proves external contract; no shared DTO assemblies |
| xUnit + architecture tests | Behavioral + dependency evidence |

---

## Architecture summary and project map

```text
src/
  Domain          Canonical Vendor/Job/Transcript + pure policies
  Core            Provider-neutral ports, Result, integration records
  Application     Use cases, disposition, eligibility, audit writers
  FieldFlow       ACL: DTOs, HTTP client, HMAC, resilience wiring
  Infrastructure  EF/SQLite, stores, classifiers
  Api             Composition root (:5203)
  PRG.FieldFlow.Mock   Local provider (:5210)
tests/
  UnitTests / IntegrationTests / ResilienceTests / ArchitectureTests
docs/
  architecture/  decisions/  assignment/  leadership/  runbooks/  packages/
```

Key docs: `docs/architecture/architecture.md` (source of `01_Architecture.pdf`), ADRs under `docs/decisions/`.

---

## Prerequisites

- .NET SDK **10.0.100+** (`global.json` uses `rollForward: latestFeature`)  
- macOS/Linux/Windows with network loopback  
- Optional: Google Chrome (only if regenerating PDFs via `scripts/render-submission-pdfs.mjs`)  
- No Docker, PostgreSQL, or live FieldFlow credentials  

---

## Configuration (placeholders only)

Copy `.env.example` or set environment variables / user-secrets. **Never commit real secrets.**

| Name | Example | Role |
|---|---|---|
| `FieldFlow__BaseUrl` | `http://localhost:5210` | Mock base URL |
| `FieldFlow__ApiKey` | `replace-me` | Connector → mock API key |
| `FieldFlow__WebhookHmacSecret` | `replace-me` | Webhook HMAC |
| `FieldFlow__ProviderInstanceId` | `fieldflow-local-1` | Instance scope |
| `FieldFlowMock__ApiKey` | `replace-me` | Must match connector key |
| `FieldFlowMock__WebhookHmacSecret` | `replace-me` | Must match connector secret |
| `FieldFlowMock__ProviderInstanceId` | `fieldflow-local-1` | Must match instance |
| `ConnectorPersistence__ConnectionString` | `Data Source=connector.dev.db` | SQLite path |
| `ConnectorPersistence__ApplyMigrationsOnStartup` | `true` | Dev convenience |
| `InboundSync__PollingEnabled` | `false` | Background poller |
| `OutboundDispatch__WorkerEnabled` | `true` (Development) | Outbox worker |
| `AdminReplay__Enabled` | `true` (Development) | Local replay gate |
| `AdminReplay__OperatorToken` | _(empty in Dev)_ | Optional token gate |

Launch profiles already set matching `replace-me` values for local demo.

---

## Commands (rehearsed)

### Restore / format / build / test

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

### Database / migrations

Development applies migrations on API startup when configured. Manual:

```bash
dotnet ef migrations add <Name> \
  --project src/PRG.Proof360.Integrations.Infrastructure \
  --startup-project src/PRG.Proof360.Integrations.Infrastructure \
  --output-dir Persistence/Migrations
```

### Run mock + connector

```bash
dotnet run --project src/PRG.FieldFlow.Mock --launch-profile http
dotnet run --project src/PRG.Proof360.Integrations.Api --launch-profile http
```

### Demo sequence

```bash
# Health
curl -s http://localhost:5210/health
curl -s http://localhost:5203/health/live
curl -s http://localhost:5203/health/ready
curl -s http://localhost:5203/connectors/fieldflow/health

# Inbound (contractors first)
curl -s -X POST http://localhost:5203/sync/contractors
curl -s -X POST http://localhost:5203/sync/work-orders

# Webhook (mock signs with shared secret)
curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"scheduled","entityVersion":2}'

# Outbound
curl -s -X POST http://localhost:5203/_demo/seed-qualified-dispatch
curl -s -X POST http://localhost:5203/connectors/fieldflow/jobs/22222222-2222-2222-2222-222222222222/dispatch
```

### Reset

```bash
curl -s -X POST http://localhost:5210/_test/reset
# Optional: stop API and delete local SQLite file (e.g. connector.dev.db)
```

### Regenerate submission PDFs (optional)

```bash
node scripts/render-submission-pdfs.mjs
```

Outputs under `docs/packages/`.

---

## Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/health/live` | Process liveness; ignores FieldFlow |
| GET | `/health/ready` | SQLite readiness; ignores FieldFlow |
| GET | `/connectors/fieldflow/health` | Sanitized connector status |
| POST | `/sync/contractors` | Poll contractors → inbox/apply |
| POST | `/sync/work-orders` | Poll work orders → inbox/apply |
| POST | `/webhooks/events` | HMAC webhook intake (`202`) |
| POST | `/connectors/fieldflow/jobs/{jobId}/dispatch` | Queue outbox dispatch |
| POST | `/_demo/seed-qualified-dispatch` | Dev seed only |
| POST | `/admin/inbox/{id}/replay` | Dev / gated replay |
| GET | mock `/health` | Mock health |
| POST | mock `/_test/reset` | Reset fixtures |
| POST | mock `/_test/failures` | Inject 429/500/timeout |
| POST | mock `/_test/webhooks/send` | Signed webhook helper |

---

## How to trigger failure / edge scenarios

| Scenario | How |
|---|---|
| **429** | `POST /_test/failures` with `rateLimitCount` / `retryAfterSeconds`, then sync |
| **500** | Same endpoint with `serverErrorCount` |
| **Timeout** | Failure injection timeout mode on mock (see mock contract doc) |
| **Duplicate webhook** | Send identical signed event twice → second `202` duplicate |
| **Out-of-order** | Send higher `entityVersion` then lower; older ignored |
| **Unknown optional field** | Fixture includes additive JSON; observed, not stored canonically |
| **Unknown contractor** | Sync work order whose contractor is absent → waiting dependency |
| **Circuit open** | Sustained 5xx via injection until health shows Offline/Open |
| **Recovery** | Clear failures / wait break duration; half-open probe succeeds |

Details: `docs/architecture/fieldflow-mock-contract.md`, `docs/architecture/resilience.md`.

---

## Assumptions and limitations

- FieldFlow schemas/signing are **ASSUMPTIONS** until PRG supplies OpenAPI.  
- SQLite + in-process workers — not multi-instance production.  
- Circuit state is process-local.  
- Admin replay is prototype-gated, not production authz.  
- No automatic payments; no Invoice/Payment canonical entities.  
- Transcript fields exist for schema lock; Phase 1 flow does not sync transcripts.  

---

## Security and secrets

- Use `.env.example` names only; put real values in env/user-secrets.  
- Never log API keys, HMAC signatures, raw webhook bodies, phone, or email.  
- Health/audit responses are sanitized (covered by tests).  

---

## Architecture decisions and production evolution

| ADR | Topic |
|---|---|
| 001 | Modular monolith + hexagonal boundaries |
| 002 | Sidecar metadata is not canonical |
| 003 | At-least-once + idempotent effects |
| 004 | SQLite prototype → PostgreSQL production |
| 005 | Inbox/outbox TX boundaries (no HTTP in DB TX) |
| 006 | Status ordering / reconciliation |
| 007 | Retry, circuit, ambiguous POST |
| 008 | Accounting-first commercial recommendation |

Production: PostgreSQL, hosted workers, vault/OAuth, shared breaker if scaled out, real alert routing — same Application ports.

---

## Documentation index

| Artifact | Location |
|---|---|
| Architecture PDF | `docs/packages/01_Architecture.pdf` (source: `docs/architecture/architecture.md`) |
| This README | `docs/packages/03_README.md` |
| Leadership PDF | `docs/packages/04_Leadership_Recommendation.pdf` |
| AI / scope notes | `docs/packages/05_AI_and_Scope_Notes.md` |
| Traceability | `docs/assignment/requirements-traceability.md` |
| Runbooks | `docs/runbooks/` |

Root `README.md` mirrors day-to-day developer commands; this file is the submission-oriented README.
