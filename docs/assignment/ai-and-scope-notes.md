# AI and Scope Notes

**Author:** Jordaine Gayle  
**Deliverable:** `05_AI_and_Scope_Notes.md`  
**Assignment:** PRG Practical Integration Assignment — FieldFlow connector prototype

---

## 1. AI tools used

| Tool | Assisted with |
|---|---|
| Cursor (agent + chat) | Scaffolding, use-case implementation, tests, docs drafting, command rehearsal |
| Kit prompts `00`–`11` | Sequential delivery structure and non-negotiable constraints |
| Model assistance inside Cursor | Boilerplate, mapping tables, ADR wording, gap analysis vs prompts |

AI did **not** invent PRG revenue, customer data, or live FieldFlow credentials. All provider traffic used the local mock and `replace-me` placeholders.

---

## 2. Personal engineering decisions (Jordaine)

These are owned decisions, not passive AI defaults:

| Decision | Choice |
|---|---|
| Canonical protection | Only Vendor/Job/Transcript; sidecar for identity/inbox/outbox/audit |
| Source of truth | Origin-dependent Job field ownership; asymmetric Vendor approval |
| Delivery semantics | At-least-once + idempotent effects — never claim exactly-once |
| Ordering / reconciliation | Entity version + payload hash; ambiguous POST reconcile before retry |
| Approval gates | No provider auto-approve; dispatch requires `qualified` + approved Vendor |
| Retry / circuit | Single HTTP retry owner; durable worker attempts separate; liveness ≠ provider |
| Phase 1 scope | Field-service reliability spine; Invoice/Payment/Appointment/Location deferred |
| Product priority | Leadership recommendation: **Accounting first**, discovery-gated (ADR-008) |
| Runtime | .NET 10 (approved deviation from kit .NET 8) |

---

## 3. Validation performed

- Manual diff review of AI-assisted changes before accepting prompt exits  
- Requirements traceability filled with real paths/tests (`docs/assignment/requirements-traceability.md`)  
- Exact-schema / architecture tests for canonical fields and forbidden dependencies  
- Unit, integration, resilience, and architecture suites (Prompt 10: 196 tests, Release, 0 warnings)  
- Local mock + API smoke (sync, health)  
- PDF visual inspection after render (Prompt 11)  

---

## 4. Intentionally deferred (and why)

| Deferred | Why |
|---|---|
| Canonical Invoice / Payment / Location / Appointment | Assignment forbids expanding Proof360 model |
| Automatic payments / funding | Control and compliance risk; Accounting MVP first |
| Production broker / multi-instance workers | Prototype uses SQLite + in-process workers |
| OAuth / vault | Documented evolution; env secrets for local demo |
| Full alert routing / dashboards | Health + metrics hooks only |
| Transcript sync | Fields locked; not driven by Phase 1 FieldFlow flow |
| Fuzzy matching | Unsafe identity strategy |

---

## 5. Adding a second provider

1. Implement Core capability ports in a new adapter project.  
2. Register in the API composition root.  
3. Keep Domain/Application free of provider DTOs.  
4. Scope uniqueness by `provider_instance_id`.  
5. Reuse disposition, outbox, health, and audit.

---

## 6. Limits of AI assistance

AI accelerates drafting and mechanical wiring; it can miss subtle ownership rules, invent confidence, or over-scope. Guardrails used: kit non-negotiables, architecture tests, typed Result/error model, and human review of reliability/security paths. **No production/customer data or real credentials were used.**
