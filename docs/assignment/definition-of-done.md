# Definition of Done (local checklist)

Derived from kit `04_DEFINITION_OF_DONE.md`. Checkboxes reflect Prompt 10 automated evidence; PDF/ZIP gates remain Prompt 11–12.

## 1. Scope and canonical governance

- [x] Only Vendor, Job, and Transcript are canonical entities (`CanonicalFieldContractTests`, `CanonicalBoundaryTests`)
- [x] Canonical field names exactly match the assignment (`CanonicalColumnTests`)
- [x] Provider DTOs do not appear in Domain or Application APIs (`FieldFlowDtoBoundaryTests`)
- [x] Provider identity uses sidecar metadata (`UniquenessConstraintTests`)
- [x] Unknown provider fields are not stored in arbitrary canonical fields (mapper optional-field tests)
- [x] AI fields are null unless genuinely produced (`No_ai_fields_are_fabricated`)
- [x] Invoice/Payment/Appointment/Location deferred (scope docs)
- [x] Provider and Proof360 SOT documented (`docs/architecture/source-of-truth.md`)

## 2. Reliability and data integrity

- [x] Inbox / identity / outbox uniqueness (`UniquenessConstraintTests`)
- [x] Duplicate poll/webhook → one effect; concurrent ≥10 tested
- [x] Status versions cannot regress; equal-version same/conflict covered
- [x] Unknown contractor deferred, resolvable, exhaustible to DLQ, replayable
- [x] No DB transaction spans provider HTTP (outbound claim/apply split; tests)
- [x] Ambiguous outbound POST reconciled before retry

## 3. Resilience and degraded operation

- [x] Bounded retries, Retry-After, permanent 4xx once, auth → NeedsAttention
- [x] Circuit open / short-circuit / half-open success and failure
- [x] Liveness independent of FieldFlow; connector health inputs covered

## 4. Security, privacy, and audit

- [x] HMAC over raw bytes + skew window; invalid webhook mutates nothing
- [x] Secrets from config; audit/health/logs redact markers
- [ ] Secret scan + ZIP inspection (Prompt 12)

## 5. Testing and code quality

- [x] Four test projects; Release TRX under `artifacts/test-results/`
- [x] Deterministic time/probes preferred over arbitrary sleeps
- [ ] Architecture/Leadership PDFs and final packaging (Prompt 11–12)

## Required Prompt 10 commands

```bash
dotnet restore
dotnet format
dotnet format --verify-no-changes
dotnet build --configuration Release --no-restore
mkdir -p artifacts/test-results
dotnet test --configuration Release --no-build \
  --results-directory artifacts/test-results \
  --logger "trx;LogFilePrefix=prg-tests"
```
