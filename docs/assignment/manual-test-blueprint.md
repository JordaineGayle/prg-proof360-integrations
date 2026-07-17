# Manual Test Blueprint (local)

Hosts should already be running (or start them yourself):

- Mock: `http://localhost:5210`
- API: `http://localhost:5203` (Development)
- DB: `artifacts/demo/manual-test.db`

All commands assume the repo root as cwd.

---

## Browser UI (recommended)

With both hosts running in Development:

1. Open [http://127.0.0.1:5203/_demo/scenarios](http://127.0.0.1:5203/_demo/scenarios)  
   (prefer `127.0.0.1` — the page proxies mock calls same-origin)
2. Click **Run all scenarios** (blueprint + invalid HMAC + concurrent dupes + approval gate + ambiguous POST + DLQ)

Endpoint catalog (Swagger UI): [http://127.0.0.1:5203/swagger](http://127.0.0.1:5203/swagger)

Deep edge cases (DLQ exhaustion, concurrent duplicates, ambiguous POST, etc.) remain in `dotnet test --configuration Release` — the page lists them.

## One-shot option

```bash
./scripts/run-demo.sh
```

That starts hosts (if needed), runs the full Prompt 13 flow, and writes `artifacts/demo/steps.json`.

---

## Step-by-step blueprint (copy/paste)

### 0) Reset

```bash
curl -s -X POST http://localhost:5210/_test/reset
```

Expect: `{"reset":true}`

### 1) Health baseline

```bash
curl -s http://localhost:5203/health/live
curl -s http://localhost:5203/health/ready
curl -s http://localhost:5203/connectors/fieldflow/health
```

Expect: live/ready Healthy; connector `status=Healthy`, `circuitState=Closed`.

### 2) Healthy sync

```bash
curl -s -X POST http://localhost:5203/sync/contractors
curl -s -X POST http://localhost:5203/sync/work-orders
curl -s http://localhost:5203/_demo/summary
```

Expect:

- contractors: `imported` ≥ 1
- work-orders: `created` ≥ 1 and `waiting` ≥ 1 (unknown contractor case)
- summary: `vendorCount≥2`, `jobCount≥2`, `inboxWaitingForDependency≥1`

### 3) Idempotent repeat

```bash
curl -s -X POST http://localhost:5203/sync/contractors
curl -s -X POST http://localhost:5203/sync/work-orders
curl -s http://localhost:5203/_demo/summary
```

Expect: `imported/created` mostly 0; vendor/job counts unchanged.

### 4) Webhook + duplicate

```bash
curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"scheduled","entityVersion":2,"eventId":"evt-demo-dup-1"}'

curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"scheduled","entityVersion":2,"eventId":"evt-demo-dup-1"}'
```

Expect: both `delivered=true`, `statusCode=202`, same `eventId`.

### 5) Newer then older (no regression)

```bash
curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"in_progress","entityVersion":5,"eventId":"evt-demo-new-5"}'

curl -s -X POST http://localhost:5210/_test/webhooks/send \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"http://localhost:5203/webhooks/events","workOrderId":"wo-2001","status":"scheduled","entityVersion":3,"eventId":"evt-demo-old-3"}'
```

Expect: both 202. Job count does not explode; older version does not regress status.

### 6) Resolve unknown contractor

```bash
curl -s -X POST http://localhost:5210/_test/contractors \
  -H 'Content-Type: application/json' \
  -d '{"contractorId":"ctr-missing-999","complianceId":"CMP-999","active":true,"license":{"number":"LIC-999","expiresOn":"2030-01-01"},"insurance":{"policy":"INS-999","expiresOn":"2030-01-01","coverage":"1000000 CAD"},"wcbNumber":"WCB-999"}'

curl -s -X POST http://localhost:5203/sync/contractors
curl -s -X POST http://localhost:5203/_demo/nudge-waiting-dependencies
curl -s http://localhost:5203/_demo/summary
```

Expect: `madeDue≥1`, `processed≥1`; summary `inboxWaitingForDependency=0`, `jobCount` increased by 1.

### 7) Circuit open + liveness

```bash
curl -s -X POST http://localhost:5210/_test/failures \
  -H 'Content-Type: application/json' \
  -d '{"serverErrorCount":20}'

for i in 1 2 3 4 5 6 7 8; do curl -s -X POST http://localhost:5203/sync/contractors >/dev/null; done

curl -s http://localhost:5203/connectors/fieldflow/health
curl -s http://localhost:5203/health/live
```

Expect: connector `Offline` / `circuitState=Open`; live still `Healthy`.

### 8) Recovery

```bash
curl -s -X POST http://localhost:5210/_test/reset
sleep 16
curl -s -X POST http://localhost:5203/sync/contractors
curl -s http://localhost:5203/connectors/fieldflow/health
```

Expect: connector back to `Healthy` / `Closed`.

### 9) Outbound dispatch (optional)

```bash
curl -s -X POST http://localhost:5203/_demo/seed-qualified-dispatch
curl -s -X POST http://localhost:5203/connectors/fieldflow/jobs/22222222-2222-2222-2222-222222222222/dispatch
sleep 2
curl -s http://localhost:5203/_demo/summary
```

Expect: seed returns fixed `jobId`; dispatch `queued`; health stays Healthy.

---

## Automated suite (separate from live demo)

```bash
dotnet test --configuration Release
```

Expect: 196 tests pass.

---

## Stop hosts

```bash
lsof -ti:5203,5210 | xargs kill
```

Or Ctrl+C in the terminals that started them.
