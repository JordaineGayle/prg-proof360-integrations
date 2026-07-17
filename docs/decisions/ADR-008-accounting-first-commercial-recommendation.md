# ADR-008: Accounting-first commercial recommendation

- Status: **Proposed**
- Date: 2026-07-17

## Context

Leadership deliverable requires choosing the first integration investment beyond (or sequenced against) field service. Decision must be assumption-transparent with measurable MVP outcomes and ROI methodology — not invented precision.

## Decision (proposed)

Recommend **Accounting** as the first commercial integration focus for finance operations (AP/AR) and implementation teams. MVP: invoice/bill creation and status sync, reconciliation, exception queue — **no automatic payment**. Field service connector remains the technical proving ground for reusable reliability patterns.

## Alternatives (why not first)

- Field service first as commercial bet — valuable ops glue but narrower finance ROI narrative for this role.
- Signal / call intelligence first — insight-heavy; weaker immediate cash/close impact without accounting backbone.
- Funding / payments first — highest risk and compliance burden; defer until accounting truth exists.

## Consequences

- Leadership PDF uses explicit assumptions, risk register, and transparent ROI formula with placeholder inputs.
- Engineering reuses inbox/outbox/idempotency lessons from FieldFlow prototype.
- Missing PRG baselines must be listed as discovery items.

## Production evolution

Pilot with one accounting provider; measure exception rate, touch time, days-to-close visibility; expand only after controls review.
