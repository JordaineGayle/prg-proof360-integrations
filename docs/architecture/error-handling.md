# Typed Result and error-handling model

Implements kit `05_TYPED_RESULT_ERROR_MODEL.md` and `.cursor/rules/error-handling.mdc`.

## Result primitive

`PRG.Proof360.Integrations.Core.Results.Result<TSuccess, TFailure>`:

- Discriminated `Succeeded` / `Failed` records (no invalid default state)
- `Match`, `Map`, `Bind`
- Null guards on `Ok` / `Fail`
- No public untyped `Value` accessor on the base type
- `Unit` for void successes

Use Result for expected outcomes. Exceptions remain for cancellation, programmer defects, and unexpected infrastructure failures.

## Provider failures

Capability ports return `Result<T, ProviderFailure>` with `ProviderFailureKind`:

Validation, Authentication, Forbidden, RateLimited, Timeout, Unavailable, CircuitOpen, ContractViolation, AmbiguousWrite, NotFound, Conflict.

FieldFlow converts expected HTTP outcomes to `ProviderFailure` once. Caller `OperationCanceledException` is rethrown when the caller token is canceled. Per-call retries, timeouts, and circuit breaking are owned exclusively by the FieldFlow resilience pipeline (ADR-007 / `docs/architecture/resilience.md`).

## Application failures

`IntegrationFailure(Code, SafeMessage, Category, RetryAfter?, ValidationErrors?, ProviderCode?)`

Stable codes live in `FailureCodes`. Categories drive disposition and HTTP mapping — never exception-message text.

`ProviderFailureTranslator` maps provider failures into application failures while preserving the original provider code for audit.

## Handled success outcomes

Duplicate receipt, stale ignore, no-change, and similar idempotent paths are **typed success outcomes** under `Application/Outcomes` (`ReceiveEventOutcome`, `ApplyWorkOrderOutcome`, …), not failures.

## Worker disposition

`FailureDispositionPolicy` centralizes durable handling:

| Disposition | Typical categories |
|---|---|
| DeadLetter | Validation, ProviderContract, Approval, exhausted retries |
| WaitForDependency | Dependency / contractor mapping missing |
| NeedsAttention | ProviderAuthentication |
| RetryAt | RateLimited, Timeout, Unavailable, PersistenceConflict, concurrency claim |

## API / RFC 7807

- `ProblemDetailsMapper` → `application/problem+json` with `type`, `title`, `detail`, `status`, `code`, `correlationId`, `retryable`, optional `retryAfterSeconds`
- `CorrelationIdMiddleware` (`X-Correlation-Id`)
- `UnexpectedExceptionMiddleware` logs once and returns sanitized `unexpected_error` (500)
- Client disconnect cancellation is not logged as failure

Never expose stack traces, connection strings, raw provider bodies, credentials, signatures, phone, or email.

## Status map (summary)

| Category | HTTP |
|---|---:|
| Validation | 400 |
| NotFound | 404 |
| Conflict / PersistenceConflict | 409 |
| Approval / Dependency / ProviderContract | 422 |
| ProviderAuthentication / RateLimited / Unavailable | 503 |
| Timeout / AmbiguousWrite | 504 |
| Unexpected | 500 |
