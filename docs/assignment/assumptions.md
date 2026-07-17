# Assumptions and Constraints

Status: Active baseline (Prompt 11)  
Delivery model: **at-least-once** with **idempotent effects**. Never exactly-once.

Connector infrastructure records (`ProviderIdentityLink`, `InboxMessage`, `OutboxMessage`, `AuditEvent`, `ConnectorState`, checkpoints) are **not** Proof360 canonical entities. They live outside the canonical domain namespace.

---

## 1. Facts supplied by the assignment / kit

- Prototype must run locally without PRG environment, live provider, customer data, or committed secrets.
- Only Proof360 canonical entities: `Vendor`, `Job`, `Transcript`. No new canonical entities or fields.
- FieldFlow is a fictional first adapter; connector boundaries must stay provider-neutral.
- FieldFlow concepts (`Contractor`, `WorkOrder`, `Appointment`, `Location`, `Invoice`, `PaymentStatus`) terminate at the anti-corruption layer.
- Required submission artifacts: Architecture PDF, prototype, README, Leadership Recommendation, AI/Scope Notes; optional demo ≤ 5 minutes.
- ZIP name: `Jordaine_Gayle_PRG_Integration_Assignment.zip`.
- Phase 1 proves: contractor import, work-order import, qualified Job dispatch, idempotent status processing, safe failure handling, health, automated tests.

### Canonical field lock (exact spellings)

**Vendor:** `vendor_id`, `compliance_id`, `license_number`, `license_expiry`, `insurance_policy`, `insurance_expiry`, `insurance_coverage`, `wcb_number`, `status`, `ai_confidence`, `missing_items`, `rationale`, `created_at`.

**Job:** `job_id`, `source`, `transcript_id`, `customer_name`, `customer_phone`, `customer_email`, `address_street`, `address_unit`, `address_city`, `address_postal`, `service_type`, `subcategory`, `priority`, `window_start`, `window_end`, `notes_scope`, `compliance_only`, `status`, `assigned_vendor_id`, `ai_confidence`, `ai_json`.

**Transcript:** `transcript_id`, `vendor_ref`, `job_ref`, `direction`, `agent_name`, `contact_phone`, `contact_email`, `call_start`, `call_end`, `duration`, `summary`, `topics`, `sentiment`, `synced_at`, `Raw_text`, `City`, `status`.

Preserve `Raw_text` and `City` capitalization if Transcript is represented. Transcript is not used by the Phase 1 connector flow.

- Never store FieldFlow IDs in `vendor_id` or `job_id`.
- Never use `ai_json`, `notes_scope`, or `rationale` as arbitrary provider-data stores.
- Never populate AI fields unless an actual AI process produced the value (Phase 1: leave null).

---

## 2. Assumptions caused by missing FieldFlow payload schemas

Labeled **ASSUMPTION** — replace if PRG supplies a real FieldFlow contract.

| Topic | Assumption |
|---|---|
| Contractor shape | Nested `license` and insurance objects; string IDs; ISO-8601 dates for expiries. |
| WorkOrder shape | Includes customer, address, service, window, contractor reference, and operational status. |
| List endpoints | Mock exposes pageable list/get for contractors and work orders. |
| Create work order | `POST /work-orders` accepts Proof360 client reference and returns `workOrderId`. |
| Idempotency header | Outbound create accepts `Idempotency-Key`; mock returns stored result for repeats. |
| Webhook envelope | Headers include provider instance, event ID, event type, event version, occurred-at, signature. |
| Additive fields | Unknown optional JSON properties are tolerated; required IDs/semantics are strict. |
| Error body | Structured error DTO with stable code/message for mock failures. |

---

## 3. Assumed nullability and validation

| Rule | Assumption |
|---|---|
| Required inbound IDs | `contractorId` / `workOrderId` missing → validation failure (not fuzzy match). |
| Dates | Parsed as UTC date/datetime; invalid format → permanent validation failure. |
| Strings | Trim whitespace; empty required string → validation failure. |
| Vendor import | Missing compliance pieces populate `missing_items`; do not invent values. |
| Job customer/address | Required for FieldFlow-originated Jobs unless assignment later softens this. |
| AI fields | Always null in Phase 1. |
| `assigned_vendor_id` | Set only when identity link resolves; otherwise defer (unknown contractor). |

---

## 4. Assumed status vocabularies

### FieldFlow WorkOrder → Proof360 Job

| FieldFlow | Proof360 Job |
|---|---|
| `open` | `dispatched` |
| `scheduled` | `scheduled` |
| `in_progress` | `in_progress` |
| `done` | `completed` |
| `void` | `cancelled` |

Proof360 also uses `qualified` before outbound dispatch.

Monotonic progress: `qualified → dispatched → scheduled → in_progress → completed`.  
`cancelled` allowed from non-terminal operational states. `completed` and `cancelled` are terminal in Phase 1.

### Vendor status (ASSUMPTION)

- First active contractor import → `pending_review`.
- Provider suspension / expired compliance may move to a restrictive status.
- Provider reactivation cannot restore `approved` without Proof360 approval gate.

---

## 5. Event ID, version, timestamp, and webhook signing

| Topic | Assumption |
|---|---|
| Event identity | Unique `(provider_instance_id, event_id)` at database level. |
| Ordering | Prefer provider `event_version` / sequence; reject ≤ last applied version as audited no-op. |
| No trustworthy version | Treat webhook as notification; reconcile by fetching current WorkOrder. |
| Wall clock | Not sole ordering guarantee. |
| Signature | HMAC-SHA256 over documented canonical signing string of **raw body**. |
| Comparison | Constant-time. |
| Timestamp skew | Reject outside configured replay window (default ASSUMPTION: ±5 minutes). |
| Ack semantics | Return `202 Accepted` only after durable inbox insert of a valid unique event. |

---

## 6. Authentication assumptions

| Environment | Assumption |
|---|---|
| Mock / prototype | Static API key + HMAC webhook secret from environment / user secrets. |
| Production discussion | Prefer OAuth 2.0 client credentials if supported; managed secret store; rotation; least privilege. |
| Logs | Never log Authorization, signatures, raw bodies, phone, or email. |

---

## 7. Provider-instance and tenancy

- Phase 1 configures **one** FieldFlow provider instance.
- All identity and inbox uniqueness keys include `provider_instance_id` so multiple accounts can coexist later without collision.
- No multi-tenant management UI in Phase 1.

---

## 8. Outbound idempotency and reconciliation

| Topic | Assumption |
|---|---|
| Idempotency key | Stable: `fieldflow:{providerInstance}:{jobId}:dispatch:v1`. |
| Outbox uniqueness | Unique `(provider_instance_id, idempotency_key)`. |
| Ambiguous POST | Timeout/connection loss after send → reconcile via client reference / identity before retry. |
| HTTP retries | Owned only by provider HTTP resilience pipeline; not nested inside another blind retry loop. |
| DB + HTTP | Never hold a DB transaction open across provider HTTP. |

---

## 9. Local environment assumptions

| Topic | Assumption |
|---|---|
| Runtime | .NET **10** SDK (`global.json` rollForward `latestFeature`). Approved deviation from kit .NET 8. |
| Persistence | SQLite for prototype; design portable to PostgreSQL. |
| Processes | Connector API + FieldFlow mock as separate local processes. |
| Workers | In-process inbox/outbox/polling workers for the prototype. |
| Data | Synthetic fixtures only. |
| Ports | Documented in README after scaffold (Swagger already exercised locally during exploration). |

---

## 10. Questions requiring PRG clarification in production

1. Official FieldFlow OpenAPI and webhook signing string definition.
2. Canonical status enumerations and transition matrix signed by product.
3. Whether appointment actuals may overwrite Proof360 desired windows.
4. Vendor approval workflow ownership and SLA.
5. Multi-account / multi-tenant FieldFlow topology.
6. Retention, encryption, and PII classification for inbox payloads.
7. Production broker, worker scale-out, and lock strategy.
8. Accounting / funding integration contracts (deferred commercially; still need product owners).
9. Baseline volumes for ROI (invoice counts, exception rates, labour rates).
10. Security review requirements (SOC2 evidence, pen-test, network egress).

---

## Explicit non-claims

- We do **not** claim exactly-once delivery.
- We do **not** claim production horizontal scale in the prototype.
- We do **not** implement Invoice, PaymentStatus, Appointment, or Location as Proof360 entities.
