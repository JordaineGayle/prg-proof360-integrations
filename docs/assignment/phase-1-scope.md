# Phase 1 Scope

Status: Draft for approval (Prompt 00)

## 1. In-scope capabilities

- Modular monolith with hexagonal boundaries (Domain, Application, Core, FieldFlow adapter, Infrastructure, API, Mock).
- Fixed canonical representations: `Vendor`, `Job` (Transcript represented only if needed for schema lock; not used in Phase 1 flows).
- Connector infrastructure: `ProviderIdentityLink`, `InboxMessage`, `OutboxMessage`, `AuditEvent`, `ConnectorState`.
- Local FieldFlow mock with deterministic success and failure injection (429, 500, timeout, duplicate, out-of-order, unknown field, unknown contractor).
- Contractor → Vendor import with source-of-truth and approval asymmetry.
- WorkOrder → Job import with origin-dependent ownership.
- Polling and webhook intake converging on one inbox application path.
- HMAC webhook verification, durable-before-ack, idempotent duplicate handling.
- Event version / stale-event handling; unknown-contractor dependency deferral; DLQ + replay.
- Qualified Job outbound dispatch via transactional outbox and stable idempotency key.
- Ambiguous POST reconciliation before retry.
- Bounded HTTP retry, Retry-After, circuit breaker; connector health states.
- Structured audit with correlation/causation; sanitized logs/metrics.
- Automated unit, integration, resilience, and architecture tests.
- Written deliverables: Architecture PDF, README, Leadership Recommendation (Accounting first), AI/Scope Notes; optional demo.

## 2. Explicitly deferred

| Capability | Reason |
|---|---|
| Canonical Invoice / Payment / Location / Appointment | Assignment forbids expanding Proof360 canonical model; FieldFlow-only DTOs if referenced. |
| Automated payments or funding actions | High risk; requires finance controls and closed-period rules. |
| Full Vendor approval UI | Policy gates in code; UI is product surface outside connector prototype. |
| Multi-provider management UI | Second provider is an adapter story, not an admin console. |
| Production message broker / distributed locks / horizontal workers | Prototype uses SQLite + in-process workers; boundaries documented for evolution. |
| Production OAuth / vault | Documented; prototype uses env/user secrets. |
| Full dashboards and alert routing | Health + metrics hooks; routing deferred. |
| Large-scale / load testing | Not required to prove architectural correctness. |
| Automated semantic conflict resolution | Conflicting non-authoritative updates are audited and ignored. |
| Transcript sync / call-intelligence flows | Canonical fields locked; Phase 1 connector does not drive Transcript. |
| Fuzzy people/address matching | Explicitly unsafe; exact external IDs only. |

## 3. Approval gates

| Gate | Phase 1 rule |
|---|---|
| Vendor promotion to `approved` | Manual / explicit Proof360 decision only. Provider reactivation cannot auto-approve. |
| Outbound dispatch | Job must be `qualified`; assigned Vendor must pass approval/compliance gates; outbox records intent first. |
| Payments / funding | **Blocked** — no automated money movement in Phase 1. |
| Replay | Allowed for dead-lettered / eligible inbox messages; replay itself must be idempotent; operator-triggered. |
| Terminal-status reopening | `completed` / `cancelled` are terminal; reopening requires a future explicit approval workflow (not implemented). |

## 4. Success definition for Phase 1

A clean local checkout can import contractors and work orders without duplicates, process valid/duplicate/stale webhooks safely, dispatch a qualified Job once, degrade on FieldFlow outage, expose connector health, and prove the above with automated tests and documentation.
