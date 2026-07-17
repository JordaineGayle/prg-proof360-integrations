#!/usr/bin/env bash
# Local five-minute demo driver (Prompt 13). No secrets; no internet.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

MOCK_URL="${MOCK_URL:-http://127.0.0.1:5210}"
API_URL="${API_URL:-http://127.0.0.1:5203}"
OUT_DIR="${DEMO_OUT_DIR:-$ROOT/artifacts/demo}"
START_HOSTS="${START_HOSTS:-1}"
CIRCUIT_WAIT_SECONDS="${CIRCUIT_WAIT_SECONDS:-16}"

mkdir -p "$OUT_DIR"
STEPS_JSON="$OUT_DIR/steps.json"
RUN_LOG="$OUT_DIR/demo-run.log"
: >"$RUN_LOG"
echo '[]' >"$STEPS_JSON"

MOCK_PID=""
API_PID=""
DB_PATH="$OUT_DIR/demo.db"

cleanup() {
  if [[ -n "${API_PID}" ]]; then kill "$API_PID" 2>/dev/null || true; fi
  if [[ -n "${MOCK_PID}" ]]; then kill "$MOCK_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT

step() {
  local id="$1"
  local title="$2"
  local narration="$3"
  shift 3
  echo ""
  echo "======== STEP ${id}: ${title} ========"
  echo "$narration"
  local body
  body="$("$@" 2>&1)" || body="ERROR: $body"
  # Redact common secret markers if any leak into output
  body="$(printf '%s' "$body" | sed -E 's/[Rr]eplace-me/[redacted]/g; s/X-FieldFlow-Signature[^,]*/X-FieldFlow-Signature:[redacted]/g')"
  printf '%s\n' "$body" | tee -a "$RUN_LOG"
  python3 - "$STEPS_JSON" "$id" "$title" "$narration" "$body" <<'PY'
import json, sys
path, sid, title, narration, body = sys.argv[1:6]
steps = json.load(open(path))
steps.append({"id": sid, "title": title, "narration": narration, "output": body})
json.dump(steps, open(path, "w"), indent=2)
print(f"[captured {sid}]")
PY
}

jq_get() {
  python3 -c "import json,sys; print(json.load(sys.stdin)$1)"
}

wait_http() {
  local url="$1"
  for _ in $(seq 1 60); do
    if curl -sf "$url" >/dev/null; then return 0; fi
    sleep 0.25
  done
  echo "Timeout waiting for $url" >&2
  return 1
}

if [[ "$START_HOSTS" == "1" ]]; then
  rm -f "$DB_PATH"
  echo "Starting mock + API for demo..."
  dotnet build --configuration Release --no-restore -v q >/dev/null 2>&1 || dotnet build --configuration Release -v q
  dotnet run --project src/PRG.FieldFlow.Mock --no-build --configuration Release \
    --urls "$MOCK_URL" >"$OUT_DIR/mock.log" 2>&1 &
  MOCK_PID=$!
  ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/PRG.Proof360.Integrations.Api --no-build --configuration Release \
    --urls "$API_URL" \
    --ConnectorPersistence:ConnectionString="Data Source=${DB_PATH}" \
    --ConnectorPersistence:ApplyMigrationsOnStartup=true \
    --FieldFlow:BaseUrl="${MOCK_URL}/" \
    --FieldFlow:ApiKey=replace-me \
    --FieldFlow:WebhookHmacSecret=replace-me \
    --InboundSync:PollingEnabled=false \
    --OutboundDispatch:WorkerEnabled=true \
    >"$OUT_DIR/api.log" 2>&1 &
  API_PID=$!
fi

wait_http "$MOCK_URL/health"
wait_http "$API_URL/health/live"

step "00" "Reset" \
  "Reset mock fixtures to a deterministic local state." \
  curl -sS -X POST "$MOCK_URL/_test/reset"

step "01" "Purpose" \
  "Provider-neutral connector foundation; FieldFlow is the first adapter. Canonical Vendor/Job only; identity/inbox/outbox are sidecar. Delivery is at-least-once with idempotent effects." \
  printf '%s\n' "Canonical: Vendor + Job. Sidecar: identity, inbox, outbox, audit. Never exactly-once."

step "02" "Health (start)" \
  "Connector health should be Healthy with circuit Closed; process liveness is independent of FieldFlow." \
  bash -c "echo LIVE:; curl -sS '$API_URL/health/live'; echo; echo READY:; curl -sS '$API_URL/health/ready'; echo; echo CONNECTOR:; curl -sS '$API_URL/connectors/fieldflow/health'"

step "03" "Sync contractors" \
  "Import contractors into Vendors with identity links." \
  curl -sS -X POST "$API_URL/sync/contractors"

step "04" "Sync work-orders" \
  "Import work orders; unknown contractor waits without creating a partial Job." \
  curl -sS -X POST "$API_URL/sync/work-orders"

step "05" "Summary after sync" \
  "Sanitized counts show Vendors, Jobs, identity links, and waiting inbox depth." \
  curl -sS "$API_URL/_demo/summary"

step "06" "Repeat sync (idempotent)" \
  "Repeating sync must not duplicate canonical rows." \
  bash -c "curl -sS -X POST '$API_URL/sync/contractors'; echo; curl -sS -X POST '$API_URL/sync/work-orders'; echo; curl -sS '$API_URL/_demo/summary'"

step "07" "Webhook accept" \
  "Send a signed status webhook for wo-2001." \
  curl -sS -X POST "$MOCK_URL/_test/webhooks/send" \
    -H 'Content-Type: application/json' \
    -d "{\"targetUrl\":\"${API_URL}/webhooks/events\",\"workOrderId\":\"wo-2001\",\"status\":\"scheduled\",\"entityVersion\":2,\"eventId\":\"evt-demo-dup-1\"}"

step "08" "Webhook duplicate" \
  "Replay the exact eventId; receipt is idempotent (still 202)." \
  curl -sS -X POST "$MOCK_URL/_test/webhooks/send" \
    -H 'Content-Type: application/json' \
    -d "{\"targetUrl\":\"${API_URL}/webhooks/events\",\"workOrderId\":\"wo-2001\",\"status\":\"scheduled\",\"entityVersion\":2,\"eventId\":\"evt-demo-dup-1\"}"

step "09" "Newer then older status" \
  "Apply a newer version, then an older version; status must not regress." \
  bash -c "curl -sS -X POST '$MOCK_URL/_test/webhooks/send' -H 'Content-Type: application/json' -d '{\"targetUrl\":\"${API_URL}/webhooks/events\",\"workOrderId\":\"wo-2001\",\"status\":\"in_progress\",\"entityVersion\":5,\"eventId\":\"evt-demo-new-5\"}'; echo; curl -sS -X POST '$MOCK_URL/_test/webhooks/send' -H 'Content-Type: application/json' -d '{\"targetUrl\":\"${API_URL}/webhooks/events\",\"workOrderId\":\"wo-2001\",\"status\":\"scheduled\",\"entityVersion\":3,\"eventId\":\"evt-demo-old-3\"}'; echo; curl -sS '$API_URL/_demo/summary'"

step "10" "Unknown contractor waiting" \
  "Summary still shows WaitingForDependency for the orphan work order." \
  curl -sS "$API_URL/_demo/summary"

step "11" "Make contractor available" \
  "Upsert the missing contractor into the local mock (demo control)." \
  curl -sS -X POST "$MOCK_URL/_test/contractors" \
    -H 'Content-Type: application/json' \
    -d '{"contractorId":"ctr-missing-999","complianceId":"CMP-999","active":true,"license":{"number":"LIC-999","expiresOn":"2030-01-01"},"insurance":{"policy":"INS-999","expiresOn":"2030-01-01","coverage":"1000000 CAD"},"wcbNumber":"WCB-999","displayName":"Resolved Demo Contractor"}'

step "12" "Import contractor + nudge waiting" \
  "Sync contractors, then nudge waiting dependencies so processing does not wait the full retry delay." \
  bash -c "curl -sS -X POST '$API_URL/sync/contractors'; echo; curl -sS -X POST '$API_URL/_demo/nudge-waiting-dependencies'; echo; curl -sS '$API_URL/_demo/summary'"

step "13" "Inject provider 500s" \
  "Deterministic failure injection to open the circuit." \
  curl -sS -X POST "$MOCK_URL/_test/failures" \
    -H 'Content-Type: application/json' \
    -d '{"serverErrorCount":20}'

step "14" "Trip circuit" \
  "Repeated sync calls until connector health shows Offline / circuit Open." \
  bash -c "for i in 1 2 3 4 5 6 7 8; do curl -sS -X POST '$API_URL/sync/contractors' >/dev/null || true; done; curl -sS '$API_URL/connectors/fieldflow/health'"

step "15" "Liveness during outage" \
  "Process liveness remains Healthy while connector is Offline." \
  bash -c "echo LIVE:; curl -sS '$API_URL/health/live'; echo; echo CONNECTOR:; curl -sS '$API_URL/connectors/fieldflow/health'"

step "16" "Restore provider" \
  "Clear injected failures and wait the documented circuit break interval for half-open recovery." \
  bash -c "curl -sS -X POST '$MOCK_URL/_test/reset'; echo; echo Waiting ${CIRCUIT_WAIT_SECONDS}s for half-open...; sleep ${CIRCUIT_WAIT_SECONDS}"

step "17" "Recovery" \
  "Successful sync closes the circuit; connector returns Healthy." \
  bash -c "curl -sS -X POST '$API_URL/sync/contractors'; echo; curl -sS '$API_URL/connectors/fieldflow/health'"

step "18" "Evidence + conclusion" \
  "Shared reliability stays in Application/Core; a second provider is a new adapter. Phase 1 excludes Invoice/Payment/Appointment/Location and automatic payments." \
  bash -c "curl -sS '$API_URL/_demo/summary'; echo; printf '%s\n' 'Second provider = new adapter on the same ports. Accounting recommended next commercially.'"

echo ""
echo "Demo capture complete."
echo "STEPS_JSON=$STEPS_JSON"
echo "RUN_LOG=$RUN_LOG"
