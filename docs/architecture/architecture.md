# Architecture — FieldFlow Connector Prototype

**Author:** Jordaine Gayle  
**Deliverable:** `01_Architecture.pdf`  
**Runtime:** .NET 10 modular monolith + local FieldFlow mock  
**Delivery model:** at-least-once + idempotent effects (never exactly-once)

---

## 1. Executive summary and key decisions

This prototype proves a reusable Proof360 connector pattern against a local FieldFlow mock: durable inbound sync (poll + webhook), canonical Vendor/Job protection, qualified outbound dispatch via outbox, bounded HTTP resilience, and operator-visible health/audit.

| Decision | Choice | Why |
|---|---|---|
| Shape | Modular monolith + hexagonal ports | Explainable Phase 1; architecture tests enforce boundaries |
| Delivery | At-least-once + idempotency | Exactly-once across HTTP/DB is not honest |
| Canonical boundary | Only Vendor / Job / Transcript | Assignment lock; connector metadata is sidecar |
| Persistence | SQLite now → PostgreSQL later | Local demo without infra drag (ADR-004) |
| TX vs HTTP | Never hold DB TX across provider I/O | Prevents stuck locks and partial dual-writes (ADR-005) |
| Commercial next | Accounting first (discovery-gated) | Finance ROI + safer than payments-first (ADR-008) |

---

## 2. Constraints and assumptions

**Facts:** only three canonical entities/fields; FieldFlow DTOs stop at the ACL; no live PRG credentials; mock shares no production DTO assembly.

**Assumptions (replace with real FieldFlow OpenAPI):** contractor/work-order JSON shapes, HMAC over `{unix}.{rawBody}`, status vocabulary, ±300s webhook skew, stable outbound idempotency key `fieldflow:{instance}:{jobId}:dispatch:v1`. Full list: `docs/assignment/assumptions.md`.

**Approved deviation:** .NET **10** (kit referenced .NET 8; only SDK 10 available here).

---

## 3. Phase 1 scope and exclusions

**In scope:** Contractor→Vendor and WorkOrder→Job import; poll+webhook inbox; HMAC; version ordering; unknown-contractor deferral; DLQ+replay; qualified Job outbox dispatch + ambiguous POST reconcile; retry/circuit/health; audit/correlation; four test projects.

**Deferred:** Invoice/Payment/Location/Appointment as canonical; automatic payments; multi-provider admin UI; production broker/OAuth/vault; full alert routing; Transcript sync; fuzzy matching. See `docs/assignment/phase-1-scope.md`.

---

## 4. Component and data flow

```text
[Operator/Demo] ──HTTP──► Connector API (:5203)
                              │
              ┌───────────────┼────────────────┐
              ▼               ▼                ▼
         Endpoints      In-process workers   Health
              │               │                │
              └──────► Application use cases ◄─┘
                         │              │
            Core ports   │              │  Sidecar + canonical
                         ▼              ▼
                   FieldFlow ACL   Infrastructure (SQLite)
                         │
                         ▼
                 FieldFlow Mock (:5210)
```

Projects: Domain (canonical + policies) → Core (ports/contracts/integration records) → Application (orchestration) → FieldFlow (ACL) / Infrastructure (EF) → API (composition). Mock has **zero** connector project references.

Diagram source: `docs/architecture/component-and-data-flow.mmd`.

---

## 5. Shared connector vs FieldFlow adapter

| Shared (Application/Core) | FieldFlow-specific |
|---|---|
| Inbox/outbox/identity/audit models | JSON DTOs + HTTP client |
| Apply/disposition/eligibility policies | HMAC verifier + normalizer |
| Failure codes / Result types | Resilience pipeline wiring |
| Capability ports | Port implementations + mappers |

A second provider adds a new adapter project implementing the same Core ports — not a fork of Application.

---

## 6. Canonical boundary vs connector metadata

Canonical types live only in Domain and use exact assignment column names.  
`ProviderIdentityLink`, `InboxMessage`, `OutboxMessage`, `AuditEvent`, `ConnectorState` are **integration infrastructure**, not new Proof360 entities (ADR-002).

Never store FieldFlow IDs in `vendor_id` / `job_id`. Never dump provider JSON into `ai_json` / `rationale` / `notes_scope`. AI fields remain null unless an actual AI process produced them (Phase 1: always null).

---

## 7. Contractor → Vendor mapping

| FieldFlow | Proof360 | Ownership |
|---|---|---|
| `contractorId` | Identity link only | External identity |
| `complianceId` / license / insurance / WCB | Matching Vendor columns | FieldFlow supplies |
| expired / missing compliance | `missing_items` + status | Deterministic Proof360 |
| `active` | May restrict | **Cannot** auto-approve |
| `displayName` | — | Ignored |
| — | `created_at` | Set once by Proof360 |
| — | `ai_confidence` | Null |

Approval asymmetry: first active import → `pending_review`; expired/restricted → `restricted`; reactivation alone cannot restore `approved`.

---

## 8. WorkOrder → Job mapping

| FieldFlow | Proof360 | Ownership |
|---|---|---|
| `workOrderId` | Identity link only | External identity |
| customer / address / service / notes / window | Job fields | Init on FieldFlow-origin; **Proof360 owns** on Proof360-origin |
| `contractorId` | `assigned_vendor_id` | Via identity resolve in Application |
| `status` | Job status | Map + monotonic transition policy |
| `entityVersion` | Sidecar / ordering | Not a canonical column |
| unknown optional JSON | Observability metadata | Never canonical storage |

---

## 9. Field and status source of truth

**Origin rule:** Proof360-originated Jobs reject provider overwrite of customer/address/service/priority/window/notes/compliance_only. After confirmed dispatch, FieldFlow is authoritative for operational status progress under the transition policy.

| FieldFlow status | Job status |
|---|---|
| `open` | `dispatched` |
| `scheduled` | `scheduled` |
| `in_progress` | `in_progress` |
| `done` | `completed` |
| `void` | `cancelled` |

Also: Proof360 `qualified` before outbound. Terminal: `completed` / `cancelled`. Unknown statuses fail validation (no fuzzy match).

---

## 10. IDs, match keys, deduplication, lineage

| Concern | Mechanism |
|---|---|
| Internal IDs | UUID `vendor_id` / `job_id` |
| External match | Unique `(provider_instance_id, entity_type, external_id)` |
| Inbox dedupe | Unique `(provider_instance_id, event_id)` |
| Outbox dedupe | Unique `(provider_instance_id, idempotency_key)` |
| Payload lineage | Payload hash + entity version on identity/inbox |
| Concurrent duplicates | DB uniqueness + claim lease |

No fuzzy people/address matching.

---

## 11. Inbound sequence and transaction boundaries

1. **Webhook:** verify HMAC over raw bytes → normalize → receipt TX (insert inbox) → `202` (accept/duplicate).  
2. **Poll/sync:** provider HTTP **outside** DB TX → synthetic event id → same receipt path.  
3. **Process:** claim TX → apply TX (canonical + identity + audit) — **no HTTP** in apply.

Sequences: `docs/architecture/inbound-sequence.mmd`, ADR-005.

---

## 12. Outbound dispatch / outbox / reconciliation

1. Eligibility: Job `qualified`, Vendor `approved`, required fields present.  
2. Outbox TX inserts pending message with stable idempotency key — **no HTTP**.  
3. Worker claims → POST work-order → on ambiguous timeout, GET by client reference (Job ID) **before** any retry with the **same** key.  
4. Complete TX: identity link + Job `dispatched` + outbox completed.

Sequence: `docs/architecture/outbound-sequence.mmd`.

---

## 13. Ordering, idempotency, dependencies, replay, DLQ

| Scenario | Behavior |
|---|---|
| Stale / older version | Audited `ignored_stale`; no regression |
| Equal version + same hash | Ignored stale / no mutation |
| Equal version + different hash | `version_payload_conflict` (no overwrite) |
| Unknown contractor | `WaitingForDependency`; no partial Job |
| Exhausted attempts/age | `DeadLettered` + failure history |
| Replay | Operator-gated; retains event identity; idempotent if already completed |

---

## 14. Failure, retry, rate limit, circuit, degraded mode

Single HTTP retry owner: FieldFlow resilience pipeline (concurrency → retry → circuit → attempt timeout → transport). Default worst-case per call: `1 + MaxRetryAttempts` (4). Workers own durable business attempts separately.

| Trigger | HTTP retry | Visible status | Durable disposition |
|---|---|---|---|
| Transient 5xx / timeout | Bounded + jitter | Degraded → Offline if circuit opens | RetryAt → DLQ when exhausted |
| 429 + `Retry-After` | Honour (capped) | Degraded on trend | RetryAt |
| Validation 400/422 | None | Unchanged | DeadLetter |
| 401/403 | None | **NeedsAttention** | NeedsAttention |
| Circuit open | Fail fast | **Offline** | RetryAt / short-circuit |
| Ambiguous POST | No blind retry | Degraded if storm | Reconcile + same key |
| Auth / circuit healthy, backlog clear | — | **Healthy** | — |

`GET /health/live` and `/health/ready` **ignore** FieldFlow availability.

---

## 15. Audit, correlation, monitoring, alerts, health

- Correlation: `X-Correlation-Id` (validated; invalid replaced).  
- Append-only `audit_events` with sanitized fields only.  
- Meter/ActivitySource `PRG.Proof360.Integrations` — low-cardinality labels only.  
- Proposed alerts (rules only): circuit Offline, auth NeedsAttention, DLQ depth, backlog age, sync freshness, 429 ratio, webhook reject spike — see `docs/architecture/observability.md`.

---

## 16. Authentication, secrets, privacy, retention

| Prototype | Production evolution |
|---|---|
| Static API key + HMAC secret via env / user-secrets | OAuth client credentials + vault + rotation |
| `.env.example` placeholders only | Managed secret store; least privilege |
| No PII/secrets in logs/health/audit | Retention policy for inbox payloads TBD with PRG |

Never log Authorization, signatures, raw bodies, phone, or email.

---

## 17. Approval gates

| Gate | Phase 1 rule |
|---|---|
| Vendor `approved` | Explicit Proof360 decision only |
| Outbound dispatch | `qualified` Job + approved Vendor |
| Payments / funding | Blocked |
| Terminal reopen | Not implemented |
| DLQ replay | Operator-triggered; idempotent |

---

## 18. Second-provider evolution

1. Add `PRG.Proof360.Integrations.<Provider>` implementing Core capability ports.  
2. Register in API composition; keep Domain/Application unchanged.  
3. Scope identity/inbox uniqueness by `provider_instance_id`.  
4. Reuse disposition, outbox, health, and audit patterns.

---

## 19. Acceptance criteria (observable)

| # | Criterion | Evidence |
|---|---|---|
| 1 | Canonical Vendor/Job/Transcript field contracts match assignment | `CanonicalFieldContractTests`, `CanonicalColumnTests` |
| 2 | Domain/Application have no FieldFlow DTO dependency | `FieldFlowDtoBoundaryTests`, `ProjectDependencyTests` |
| 3 | Repeat contractor poll → one Vendor + identity link | `Repeating_contractor_snapshot_yields_one_vendor_and_link` |
| 4 | Unknown contractor waits; no partial Job; exhaustion → DLQ | `Unknown_contractor_waits_*`, `Exhausted_unknown_contractor_*` |
| 5 | Valid webhook durable before ack; invalid HMAC mutates nothing | `WebhookSecurityTests` |
| 6 | ≥10 concurrent duplicate webhooks → one inbox + one Job | `Concurrent_duplicate_webhooks_create_one_inbox_and_one_job` |
| 7 | Newer status applies; older after terminal cannot regress | `Newer_status_then_terminal_blocks_older_event_regression` |
| 8 | Equal-version same payload no mutation; different → conflict | `Equal_version_*` |
| 9 | Outbox dispatch once; ambiguous POST reconciles without duplicate | `OutboundDispatchTests` |
| 10 | Retry-After / circuit / liveness independent of FieldFlow | `HttpResiliencePipelineTests`, `HostHealthEndpointTests` |
| 11 | DLQ replay idempotent; audit omits secrets/PII markers | `AuditReplayObservabilityTests` |
| 12 | Local mock + API sync + connector health without credentials | Prompt 10 smoke / this README demo |

---

## 20. Tradeoffs and production evolution

| Tradeoff | Phase 1 | Production |
|---|---|---|
| SQLite + in-process workers | Fast local proof | PostgreSQL + hosted workers / broker |
| Static secrets | Demo safety | Vault + rotation |
| In-memory circuit state | Process-scoped | Shared/distributed breaker if multi-instance |
| Admin replay token gate | Local ops | Real authz + change control |
| Accounting deferred in code | Field service proves patterns | Next commercial bet (ADR-008) |

**Residual risks:** assumed FieldFlow schemas; single-instance workers; no load test; alert routing not wired.

---

## Document index

| Topic | Path |
|---|---|
| Source of truth | `docs/architecture/source-of-truth.md` |
| Schema mapping | `docs/architecture/schema-mapping.md` |
| Resilience | `docs/architecture/resilience.md` |
| Observability | `docs/architecture/observability.md` |
| Error model | `docs/architecture/error-handling.md` |
| ADRs | `docs/decisions/ADR-001` … `ADR-008` |
| Traceability | `docs/assignment/requirements-traceability.md` |
