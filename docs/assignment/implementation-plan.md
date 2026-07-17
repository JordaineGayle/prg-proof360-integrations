# Implementation Plan

Aligned with `00_MASTER_EXECUTION_PLAN.md` and kit prompts `00`–`12`.  
Delivery semantics: **at-least-once + idempotent effects** (never exactly-once).

## Current repository state (after Prompt 10)

- Modular monolith on **.NET 10** with canonical models, FieldFlow mock, capability ports, ACL mappers, SOT docs, inbound sync/webhooks, outbound outbox, resilience/health, audit/replay/metrics.
- `Result<TSuccess, TFailure>`, typed failures/dispositions, RFC 7807 Problem Details, and outer exception middleware.
- Full automated suite + filled traceability; submission docs in `docs/packages/` (Prompt 11). ZIP packaging remains Prompt 12.

## Phases, exit gates, and evidence

| Phase | Kit prompt | Outputs | Exit gate | Evidence |
|---|---|---|---|---|
| 0 Lock scope | `00` | Assumptions, scope, plan, ADR shells, discussion guide | Unspecified contracts labeled; no new canonical fields; no code scaffold | `docs/assignment/*`, `docs/decisions/*` |
| 1 Scaffold | `01` | `src/` / `tests/` layout, central packages, analyzers, health/test skeletons | Restore, format, build, empty tests pass | Solution + Directory.*.props + CI-friendly commands |
| 2 Persistence | `02` | Canonical + infrastructure EF schema, uniqueness, migrations | Tests prove fields + DB dedupe | Architecture/integration persistence tests |
| 3 FieldFlow mock | `03` | Mock endpoints, fixtures, failure injection, Idempotency-Key, HMAC model | Mock runs without credentials; failures deterministic | Mock project + contract fixtures |
| 4 Mapping | `04` | Capability ports, FieldFlow adapter, mappers, SOT/status policies | Mapping/status/schema-evolution unit tests pass | Unit + mapping tables in docs |
| 5 Inbound | `05`–`06` | Polling, verified webhooks, inbox, deferral, ordering, DLQ/replay | One application path; duplicate/concurrent tests pass | Integration idempotency/ordering/dependency tests |
| 6 Outbound | `07` | Qualified dispatch, outbox, reconciliation, identity linkage | Repeated dispatch cannot duplicate WorkOrders | Outbox/dispatch integration tests |
| 7 Resilience/ops | `08`–`09` | Retry/circuit/health, audit, metrics, sanitized failures | Outage → open → half-open → recovery demonstrated | Resilience + health tests |
| 8 Docs & harden | `10`–`11` | Full suite, Architecture PDF, README, Leadership PDF, AI notes, traceability | Every requirement has evidence | `03_REQUIREMENTS_TRACEABILITY` filled |
| 9 Package | `12` (+ optional `13` demo) | Release build, secret scan, ZIP | Exact ZIP name/shape; no bin/obj/git/secrets | `04_DEFINITION_OF_DONE` checklist |

## Deliverables coverage

| Review / deliverable | Plan location |
|---|---|
| Architecture & systems design (40%) | Blueprint implementation Phases 1–7; ADRs; Architecture PDF |
| Business / product reasoning (20%) | ADR-008 + Leadership Recommendation (Accounting first) |
| Reliability / ops (15%) | Phases 5–7; runbooks |
| Testing (10%) | Four test projects; Prompt 10 |
| Documentation (10%) | Prompts 11–12; discussion guide |
| Code quality (5%) | Analyzers, XML docs, small modules from Prompt 01 onward |
| Working prototype | Mock + API + workers |
| Traceability | Copy/maintain matrix into `docs/assignment/requirements-traceability.md` before packaging |

## Operating discipline

- One kit prompt per agent run; review diff; run listed validation commands; fix failures before next prompt.
- Update ADRs from `Proposed` → `Accepted` only when the decision is confirmed in code/docs.
- Behavioral requirements require a test or reproducible demo step — not “documented only.”

## Remaining kit prompts

- Prompt 13 (optional): short demo video ≤ 5 minutes
