# Cursor Prompt 10 - Complete Test Suite and Quality Hardening

---

Audit the entire prototype against the assignment and complete the automated test suite. Fix correctness, determinism, maintainability, and documentation gaps discovered by the tests. Do not add unrelated features.

## Inspect first

Read the traceability matrix, Definition of Done, all source/test projects, ADRs, and current command results. Produce a concise gap list grouped by assignment requirement before editing.

## Required test categories

Ensure the suite contains clear evidence for:

### Canonical and architecture

- Exact Vendor, Job, and Transcript field contracts.
- Forbidden project dependencies.
- No FieldFlow DTO leakage into Domain/Application.
- No provider ID or arbitrary provider payload fields in canonical types.

### Mapping and governance

- Contractor and WorkOrder happy paths.
- Missing/expired compliance and approval asymmetry.
- Origin-dependent field ownership.
- Unknown additive optional field.
- Required-field and unknown-status failure behavior.
- All allowed/invalid/terminal status transitions.

### Idempotency and ordering

- Repeated polling.
- Duplicate webhook.
- Poll/webhook race.
- Ten or more concurrent duplicates.
- Equal-version same payload.
- Equal-version conflicting payload.
- Older event after terminal state.
- Database transaction rollback with no partial effect.

### Dependencies, retry, and replay

- Unknown contractor wait, resolution, exhaustion, and replay.
- 429 with Retry-After.
- 500 and timeout recovery.
- Permanent 400 and authentication 401/403.
- Caller cancellation.
- Dead-letter classification and idempotent replay.

### Circuit, health, and observability

- Open, short-circuit, half-open success/failure, close.
- Liveness independent from provider availability.
- Connector status projection.
- Backlog, dead-letter, unresolved dependency, and freshness inputs.
- Audit outcomes and correlation propagation.
- PII/secret redaction.
- Low-cardinality metric dimensions.

### Outbound dispatch

- Eligibility/approval gates.
- Concurrent request outbox uniqueness.
- Stable idempotency key.
- Provider success and local atomic completion.
- Ambiguous provider success reconciled without duplication.
- Failed local completion recovered idempotently.

## Test quality

- Replace real wall-clock dependencies with injected time.
- Remove arbitrary sleeps; use deterministic worker execution or bounded async probes.
- Isolate database state per test.
- Ensure failure injection is deterministic.
- Assert provider call counts for retry/circuit tests.
- Assert database state and audit evidence, not only HTTP status.
- Give tests behavior-focused names and useful failure messages.
- Avoid excessive mocking of the code under test.

## Code hardening

While resolving test gaps:

- Remove dead code and unused abstractions.
- Resolve analyzer warnings rather than suppressing them.
- Check cancellation propagation.
- Check transaction scopes and disposal.
- Check every log template for sensitive/high-cardinality values.
- Check all configuration validation and safe defaults.
- Ensure public APIs have useful XML docs and non-obvious logic has why-comments.
- Keep the prototype small; document production extensions instead of implementing them.

## Evidence output

- Configure test result output under `artifacts/test-results/`, excluded from final prototype packaging unless a concise evidence file is intentionally included.
- Optionally collect coverage to locate untested critical paths. Do not optimize for a vanity percentage or include generated reports in the submission.
- Update `docs/assignment/requirements-traceability.md` with actual source paths and test names.

## Required commands

Run from a clean working state:

```bash
dotnet restore
dotnet format
dotnet format --verify-no-changes
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --logger "trx;LogFileName=prg-tests.trx"
```

Also run the documented local smoke flow against the real connector and mock processes. Do not rely exclusively on in-memory unit tests.

## Do not

- Do not weaken assertions to make failures disappear.
- Do not delete a required behavior because it is hard to test.
- Do not add unrelated production infrastructure.
- Do not claim coverage for a requirement without pointing to evidence.
- Do not leave skipped tests without an explicit reviewed reason.

## Completion report

Report initial gaps, fixes, final test inventory by requirement, command outputs, skipped/deferred items, residual risks, and updated traceability. Stop only when release build and full tests pass or report the exact blocker.
