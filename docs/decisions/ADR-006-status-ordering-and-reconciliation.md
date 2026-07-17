# ADR-006: Status ordering and reconciliation

- Status: **Accepted**
- Date: 2026-07-17
- Updated: 2026-07-17 (Prompt 06 webhook ordering)

## Context

Provider events can arrive late or out of order. Wall-clock timestamps alone are unsafe. Missing versions still need a deterministic policy. Webhooks and polls must share one apply path.

## Decision

1. **Version ordering** — Prefer provider `entityVersion`. Compare to `ProviderIdentityLink.LastAppliedVersion`.
   - `incoming < last` → audited `ignored_stale` (success, no mutation).
   - `incoming == last` and payload hash differs → audited `version_payload_conflict` (security anomaly, no mutation).
   - `incoming == last` and hash matches → audited `ignored_stale`.
   - `incoming > last` → apply, then validate status transition.
2. **Transition validation** — Independently evaluate `JobStatusTransitionPolicy`. Invalid transitions are audited no-ops (`ignored_invalid_transition`), not endless retries. Terminal states (`completed`, `cancelled`) do not reopen in Phase 1.
3. **Missing/untrusted version** — If webhook entity version is absent or `<= 0`, treat the webhook as a **notification** and fetch current WorkOrder via FieldFlow GET. Tradeoff: apply current provider state, not a possibly stale envelope body.
4. **Status vocabulary** — FieldFlow→Job map lives in `WorkOrderStatusMappingPolicy` (`docs/architecture/source-of-truth.md`).

## Alternatives

- Last-write-wins by timestamp — allows regressions.
- Blind apply every event — non-monotonic operational state.
- Drop events without version — silent data loss; prefer reconcile.

## Consequences

- Stale/conflict events are safe handled outcomes, not HTTP 500 or infinite retries.
- Identity links store `last_applied_version` and `payload_hash`.
- Unsupported webhook types/schemas are stored then dead-lettered for inspection.

## Production evolution

Product-signed transition matrix; optional explicit reopen workflow with audit and authorization; distributed replay tooling for DLQ.
