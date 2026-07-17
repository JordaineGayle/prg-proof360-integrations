# Source of truth

Normalized connector snapshots (`ContractorSnapshot`, `WorkOrderSnapshot`) are **integration contracts**, not Proof360 canonical entities. They may carry external identity/version metadata for processing but must never be persisted as `Vendor` / `Job` / `Transcript`.

---

## Contractor → Vendor

| FieldFlow source | Proof360 target | Ownership |
|---|---|---|
| `contractorId` | Sidecar identity link | FieldFlow identity; never `vendor_id` |
| `complianceId` | `compliance_id` | FieldFlow supplies |
| `license.number` / `expiresOn` | `license_number` / `license_expiry` | FieldFlow supplies; invalid dates fail validation |
| insurance fields | insurance_* | FieldFlow supplies; expired → safe-deny (`restricted`) |
| `wcbNumber` | `wcb_number` | FieldFlow supplies |
| `active` | status policy input | May restrict; **cannot** auto-approve |
| missing compliance | `missing_items` | Deterministic Proof360 calculation |
| — | `ai_confidence` | Always null in Phase 1 |
| policy decision | `rationale` | Concise Proof360 text |
| import time | `created_at` | Proof360-owned; set once on insert |

`displayName` has no safe canonical meaning → **ignored**.

### Approval asymmetry

1. First active import → `pending_review`
2. Expired compliance / provider restriction → `restricted`
3. Provider reactivation alone cannot restore `approved`

---

## WorkOrder → Job

| FieldFlow source | Proof360 target | Ownership |
|---|---|---|
| `workOrderId` | Sidecar identity link | Never `job_id` |
| inbound origin | `source=FieldFlow` | Set on FieldFlow-originated create |
| customer / address / service / notes | corresponding Job fields | Initialize FieldFlow-origin Jobs; **Proof360 owns** these for Proof360-origin Jobs |
| `windowStart` / `windowEnd` | desired window | Proof360 owns for outbound Jobs; do not treat as appointment actuals |
| `contractorId` | `assigned_vendor_id` | Resolve via identity map in Application (mapper never queries DB) |
| `status` | `status` | FieldFlow operational outcome after dispatch, via status map + transition policy |
| — | `ai_confidence` / `ai_json` | Null; never fabricate |
| — | `priority` / `compliance_only` | No FieldFlow semantic → leave default/null |
| `entityVersion` / schema | sidecar / processing metadata | Carried on snapshot; not canonical columns |

### Origin rule

- **Proof360-originated** Job: provider echoes cannot overwrite customer, address, service, priority, desired window, notes_scope, or compliance_only. Conflicts are ignored (surfaced as ignored-field metadata for audit). Outbound dispatch sends these Proof360-owned fields; create responses do not write them back onto the Job.
- **FieldFlow-originated** Job: FieldFlow may initialize those fields.
- After successful dispatch, FieldFlow is authoritative for **operational status progress** subject to the monotonic transition policy. Job becomes `dispatched` only after confirmed/reconciled create (outbox complete + identity link).

### Outbound eligibility (summary)

Dispatch requires `qualified` status, required customer/address/service fields, window or notes scope, assigned Vendor that is Proof360-`approved` (not `restricted`), and no existing work-order identity/outbox for the stable idempotency key. Details: `docs/architecture/outbound-dispatch.md`.

---

## Status mapping (FieldFlow → Job)

| FieldFlow | Proof360 Job |
|---|---|
| `open` | `dispatched` |
| `scheduled` | `scheduled` |
| `in_progress` | `in_progress` |
| `done` | `completed` |
| `void` | `cancelled` |

Proof360 also uses `qualified` before outbound dispatch. Unknown provider statuses fail with stable validation classification (no fuzzy match).

### Conflict examples

| Scenario | Result |
|---|---|
| Provider sends `done` while Job is `qualified` without intermediate states | Transition rejected; audited no-op/failure (not silent overwrite) |
| Stale `entityVersion` ≤ last applied | Ordering no-op (Prompt 05/06); mapping still validates status independently |
| Proof360 Job customer differs from provider echo | Customer retained; field listed in ignored ownership set |
| Provider `active=true` on new Vendor | `pending_review`, never `approved` |
