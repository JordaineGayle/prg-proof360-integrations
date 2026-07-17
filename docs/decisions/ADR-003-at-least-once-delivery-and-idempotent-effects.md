# ADR-003: At-least-once delivery and idempotent effects

- Status: **Accepted**
- Date: 2026-07-17

## Context

Webhooks and polling can duplicate. Networks retry. Claiming exactly-once across systems is dishonest.

## Decision

Model transport as **at-least-once**. Enforce **idempotent canonical effects** via database uniqueness (inbox event ID, identity links, outbox idempotency keys) plus transactional upserts in later prompts. Never claim exactly-once delivery.

## Alternatives considered

- Exactly-once messaging — not achievable end-to-end with an external provider.
- In-memory dedupe only — fails under concurrency and restart.
- Separate mapping logic per channel — drift risk.

## Consequences

- Duplicates are expected and safe.
- Correctness depends on DB constraints (proven by persistence tests).
- Documentation must never say “exactly-once.”

## Production evolution

Same invariants with a broker; consumers remain idempotent; uniqueness constraints stay authoritative.
