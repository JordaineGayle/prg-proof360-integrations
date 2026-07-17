# Cursor Prompt 09 - Audit, Correlation, Dead-Letter Replay, and Observability

---

Complete structured audit, correlation propagation, dead-letter/replay behavior, metrics, and operational runbooks.

## Inspect first

Read existing audit records, inbox/outbox states, middleware, workers, health policy, security rules, and every failure category. List gaps and proposed evidence before editing.

## Correlation and causation

- Accept a valid bounded correlation ID header or generate one.
- Generate operation/event causation IDs at meaningful boundaries.
- Propagate correlation through API, inbox/outbox, HTTP request headers, retries, audit, logs, and health diagnostics where safe.
- Preserve one correlation ID across retries of the same logical operation.
- Do not trust unlimited/unsafe caller-supplied header text; validate length/characters.

## Structured audit

Record append-only audit events for:

- Sync requested/completed/failed.
- Entity created/updated/no-change/restricted.
- Webhook accepted/duplicate/signature rejected/stale/unsupported.
- Status applied/ignored/invalid.
- Dispatch requested/succeeded/ambiguous/reconciled/failed.
- Dependency waiting/resolved/exhausted.
- Circuit opened/half-open/closed where hooks permit.
- Dead-lettered and replay requested/completed.

Include provider, direction, operation, internal/external references, event, correlation/causation, attempt, outcome, latency, error category, schema version, payload hash, and timestamp when applicable.

Do not store raw secrets or unnecessary PII. Audit is evidence, not a raw data lake.

## Dead-letter and replay

- Define deterministic permanent versus retryable processing failures.
- Move exhausted/unsupported/invalid-schema work to visible `DeadLettered` state.
- Preserve original event identity, payload hash, failure history, and correlation.
- Expose a local/admin replay endpoint or command requiring operator identity/reason in the request.
- Replay creates a new replay attempt/causation identity but retains original event identity so canonical effects remain idempotent.
- Do not overwrite failure history.
- Prevent replay of non-dead-letter work unless explicitly designed.
- Document production authorization/approval requirements; the prototype endpoint must be clearly local/admin-only.

## Metrics and traces

Use structured logging plus OpenTelemetry/BCL metrics/activities as already selected. Add low-cardinality metrics for:

- Provider request count/duration by operation/outcome.
- Retry, rate-limit, and circuit transitions.
- Webhook accepted/rejected/duplicate/stale.
- Inbox/outbox processed/failed/dead-lettered and backlog age.
- Unresolved dependencies.
- Sync freshness.

Never use customer IDs, external IDs, correlation IDs, phone/email, or raw error messages as metric labels.

Add activity spans around use cases, provider calls, and worker processing with safe tags.

## Alerting and visible status documentation

Document proposed alerts for:

- Circuit open/provider offline.
- Authentication/configuration failure.
- Dead-letter depth greater than zero.
- Oldest inbox/outbox age above threshold.
- Sync freshness breach.
- Elevated 429 or transient error ratio.
- Webhook signature rejection spike.

Include severity, threshold assumption, owner, immediate action, and false-positive consideration. No external alert service is required in Phase 1.

## Required tests

1. Correlation generated when missing and accepted when valid.
2. Invalid/oversized correlation is replaced safely.
3. Correlation persists through webhook/inbox and dispatch/outbox retries.
4. Required audit outcomes are created with stable fields.
5. Sensitive marker test proves logs/audit/health do not leak configured secrets/PII.
6. Permanent/unsupported event is dead-lettered.
7. Exhausted transient/dependency event is dead-lettered.
8. Replay retains original event identity, adds operator/reason/causation, and remains idempotent.
9. Unauthorized/non-local replay behavior matches documented prototype decision.
10. Metric dimensions remain low-cardinality.
11. Backlog/dead-letter metrics affect connector health.

## Documentation

- Create `docs/runbooks/replay-and-dead-letter.md`.
- Complete observability/security architecture section.
- Add proposed alert table.
- Update README with safe audit/health/replay examples.

## Do not

- Do not treat raw payload storage as audit.
- Do not put high-cardinality IDs into metric labels.
- Do not erase dead-letter history during replay.
- Do not allow replay to bypass canonical idempotency or approval rules.
- Do not claim an alerting backend is implemented if only metrics/rules are provided.

## Completion report

Report audit event coverage, correlation propagation, dead-letter classification, replay approval/idempotency, metrics, alerts, tests, commands, and deferred production controls. Stop after observability tests pass.
