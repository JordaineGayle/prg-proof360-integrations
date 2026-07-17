# Cursor Prompt 11 - Architecture, README, Leadership, and AI/Scope Artifacts

---

Create the final written submission artifacts from the implemented and tested system. Every claim must match the code and test evidence. Keep the documents concise, visual, and defensible.

## Inspect first

Read the original assignment, final traceability matrix, ADRs, source, tests, runbooks, command results, and implementation limitations. List any disagreement between documentation and behavior before drafting.

## A. Architecture source and `01_Architecture.pdf`

Create a polished architecture source document with this structure:

1. Executive summary and key decisions.
2. Assignment constraints and explicit assumptions.
3. Phase 1 scope and intentional exclusions.
4. Component/data-flow diagram.
5. Shared connector versus FieldFlow adapter responsibilities.
6. Fixed canonical boundary and connector metadata distinction.
7. Contractor-to-Vendor mapping table.
8. WorkOrder-to-Job mapping table.
9. Field-level and status-level source-of-truth tables.
10. Internal/external IDs, match keys, deduplication, and lineage.
11. Inbound webhook/polling sequence and transaction boundaries.
12. Outbound dispatch/outbox/reconciliation sequence.
13. Ordering, idempotency, dependency deferral, replay, and DLQ.
14. Failure classification, retry, rate limit, circuit, and degraded mode.
15. Audit, correlation, monitoring, alerts, health, and visible status.
16. Authentication, secret handling, privacy, and retention.
17. Approval gates.
18. Second-provider evolution.
19. At least ten observable acceptance criteria mapped to test/demo evidence.
20. Tradeoffs and production evolution.

Use diagrams for topology and event order, and tables for exact mappings/rules. Keep diagrams legible when printed. Target roughly 8-12 focused pages rather than a large design book.

Generate `01_Architecture.pdf` through the repository's reproducible document workflow when available. If required render tooling is missing, create print-ready source/HTML and report the exact dependency rather than generating a fake or broken PDF.

## B. Final `03_README.md`

Create a standalone README at the final-artifact location containing:

- Purpose and demonstrated behaviors.
- Technology choices and reasons.
- Architecture summary and project map.
- Exact prerequisites.
- Exact environment/configuration names with placeholder values.
- Restore, migration/database, run-mock, run-connector, test, demo, reset, and troubleshooting commands.
- Endpoint table and safe examples.
- How to trigger 429, 500, timeout, duplicate, out-of-order, unknown field, unknown contractor, circuit, and recovery.
- Assumptions and limitations.
- Security and secret guidance.
- Architecture decisions and production evolution.
- Documentation index.

Rehearse every command; do not include commands that were not tested.

## C. `04_Leadership_Recommendation.pdf`

Write a concise decision memo recommending **Accounting first**, subject to discovery validation.

Required content:

- Decision and target users: finance operations/AP-AR, implementation/support, and customers needing accounting synchronization.
- Business value: automated transaction creation, lower manual touch/error cost, faster reconciliation/close/cash visibility, retention and expansion.
- MVP: invoice/bill transaction creation, sync status, reconciliation and exception handling; human approval for high-risk actions; no automatic payment movement.
- High-level sequence and timeline assumptions, for example discovery/data contract, core transaction sync, exceptions/reconciliation, controlled pilot, scale decision.
- Dependencies, risks, and assumptions including closed accounting periods and supplemental transactions.
- Quantified success measures with thresholds clearly labeled as targets/assumptions.
- ROI methodology:

  `Annual benefit = volume x minutes saved x loaded labour rate + error costs avoided + cash-flow/retention benefit - build and operating cost`

- Why Field service, Signal ingestion, Call intelligence, and Funding should not be first, without dismissing their value.
- Information required before final commitment: volume, time, exception/error, provider/customer concentration, willingness to pay, API/security/SLA, and delivery-capacity data.

Use the prior interview signal that transaction creation must precede payments and that invoice automation requires approval boundaries. Do not invent PRG revenue, costs, or volumes.

Target 2-4 polished pages.

## D. `05_AI_and_Scope_Notes.md`

Record honestly:

- AI tools used and the tasks they assisted with.
- Jordaine's personal decisions: canonical protection, source of truth, at-least-once/idempotency, ordering/reconciliation, approval gates, retry/circuit thresholds, Phase 1 scope, and product priority.
- Validation through manual diff review, assignment traceability, exact-schema tests, unit/integration/resilience tests, build/analyzer results, local demonstration, and PDF visual inspection.
- What was intentionally deferred and why.
- How a second provider is added.
- Limitations of AI assistance and confirmation that no production/customer data or credentials were used.

Keep this brief and factual.

## PDF generation and visual QA

- Generate PDFs only from finalized reviewed source.
- Render every page to images or inspect through a reliable PDF viewer.
- Check clipping, page breaks, diagrams, tables, fonts, glyphs, page numbers, whitespace, and headings.
- Fix all visible defects and regenerate.
- Confirm selectable/readable text where possible.

## Consistency review

Search all artifacts for:

- Contradictory retry counts/status names/timelines.
- Claims of exactly-once delivery.
- Claims of implemented production systems that are deferred.
- Unlabeled assumptions or invented ROI figures.
- New canonical entities/fields.
- Secrets, real data, machine-specific paths, or AI-generated placeholders.

## Completion report

Report generated source/PDF paths, page counts, command rehearsal, visual QA results, consistency findings, remaining limitations, and every assignment section covered. Do not package yet.
