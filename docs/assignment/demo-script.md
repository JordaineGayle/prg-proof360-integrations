# Five-Minute Demo Script

**Artifact:** optional `06_Demo.mp4` (≤ 5:00)  
**Driver:** `scripts/run-demo.sh` (Windows: `scripts/run-demo.ps1`)  
**Working directory:** solution root (`02_Prototype/` inside the submission ZIP)

## Narration outline

| Time | Beat | What to show |
|---|---|---|
| 0:00–0:30 | Purpose | Provider-neutral connector; FieldFlow first adapter; Vendor/Job canonical + sidecar inbox/outbox/identity; at-least-once + idempotent effects |
| 0:30–1:30 | Healthy sync | Connector Healthy → sync contractors → sync work-orders → `/_demo/summary` → repeat sync (counts stable) |
| 1:30–2:20 | Webhooks | Signed status event → exact `eventId` duplicate → older version does not regress |
| 2:20–3:10 | Dependency | Unknown contractor wait → upsert contractor → nudge waiting → one assigned Job |
| 3:10–4:20 | Outage | Inject 500s → circuit Open / Offline → `/health/live` still Healthy → reset → wait break duration → Healthy |
| 4:20–4:50 | Close | Summary + second-provider adapter story + Phase 1 exclusions |

Leave ~10s margin. Prefer shortening narration over rushing commands.

## Preconditions

- .NET 10 SDK  
- Ports free (defaults `5210` mock / `5203` API) or override via env  
- Placeholder secrets only (`replace-me`)  
- No internet required  

## Rehearsal checklist

- [ ] Two full runs from reset  
- [ ] No secret entry on screen  
- [ ] Failure/recovery finishes inside window  
- [ ] Health/summary sanitized (no phone/email/signatures)  
- [ ] Final MP4 ≤ 5:00 and plays cleanly  

## Recording notes

Prefer a clean terminal font, hide personal paths, and avoid scrolling source. The packaged video may be a captioned evidence reel generated from `scripts/render-demo-video.sh` after a live `run-demo.sh` capture.
