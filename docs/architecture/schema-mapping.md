# Schema mapping (FieldFlow ACL)

The FieldFlow adapter (`PRG.Proof360.Integrations.FieldFlow`) owns provider DTOs and HTTP. Application consumes **provider-neutral snapshots** from `PRG.Proof360.Integrations.Core.Providers.Contracts`. Domain canonical types never reference FieldFlow DTOs.

```text
FieldFlow JSON DTO
  → FieldFlow*Mapper (validation + unknown-field capture)
  → ContractorSnapshot / WorkOrderSnapshot  (integration contract)
  → Application ContractorToVendorMapper / WorkOrderToJobMapper
  → Vendor / Job  (canonical)
```

Snapshots are **not** persisted as Proof360 entities. See `source-of-truth.md`.

---

## Capability ports (Core)

| Port | Purpose |
|---|---|
| `IContractorSnapshotSource` | List/get contractor snapshots |
| `IWorkOrderSnapshotSource` | List/get work-order snapshots |
| `IWorkOrderDispatcher` | Idempotent create |
| `IWorkOrderReconciler` | Read by external id / client reference |
| `IWebhookVerifier` | HMAC + timestamp + instance checks |
| `IProviderCapabilities` | Discovery flags (no `NotImplementedException` for unsupported ops) |

FieldFlow Phase 1 supports all of the above. Ports return `Result<T, ProviderFailure>`. Unsupported operations use `ProviderFailure.Unsupported(...)`. See `docs/architecture/error-handling.md`.

---

## Contractor mapping detail

| Snapshot field | Canonical | Notes |
|---|---|---|
| ExternalContractorId | identity link only | Opaque string |
| ComplianceId / license / insurance / WCB | Vendor columns | Trimmed; dates as `DateOnly` |
| IsActive + expiry | VendorApprovalPolicy | Asymmetric approval |
| UnknownOptionalFields | adapter metadata / telemetry | Never copied into Vendor |
| — | `created_at` | Injected `IClock` on first insert only |
| — | AI fields | Always null |

**Ignored provider fields:** `displayName`.

---

## WorkOrder mapping detail

| Snapshot field | Canonical | Notes |
|---|---|---|
| ExternalWorkOrderId | identity link only | |
| ClientReference | reconciliation key | Proof360 Job ID string on outbound |
| ExternalContractorId | assigned_vendor_id via Application identity resolve | Mapper does not query EF |
| ProviderStatus | Job.status via `WorkOrderStatusMappingPolicy` | Unknown → validation failure |
| Customer/address/service/notes/window | Job fields | Origin-dependent ownership |
| EntityVersion / SchemaVersion | processing metadata | Carried for inbox/ordering |
| UnknownOptionalFields | observable metadata | Not canonical storage |
| — | transcript / AI | Null |

**Deferred provider surfaces:** Invoice, PaymentStatus, Location, Appointment (no canonical mapping).

---

## Validation classifications

| Condition | `ProviderFailureKind` / code |
|---|---|
| Missing `contractorId` / `workOrderId` | `Validation` / `missing_*_id` |
| Invalid license/insurance date | `Validation` / `invalid_date` |
| Unknown status at Application map | mapping failure reason (not fuzzy) |
| 401 from provider | `Authentication` |
| 429 | `RateLimited` (+ Retry-After when present) |
| Timeout / lost create response | `Timeout` / `AmbiguousWrite` |

---

## Additive optional JSON

`[JsonExtensionData]` on FieldFlow DTOs keeps deserialization tolerant. Unknown property names are listed on the snapshot for observability and must not be written into canonical columns.
