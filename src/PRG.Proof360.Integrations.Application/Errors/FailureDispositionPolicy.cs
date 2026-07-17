namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Maps typed <see cref="IntegrationFailure"/> values to durable worker dispositions.
/// </summary>
public sealed class FailureDispositionPolicy
{
    /// <summary>
    /// Decides how a worker should persist state after a typed failure.
    /// </summary>
    public FailureDisposition Decide(IntegrationFailure failure, FailureDispositionContext context)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsExhausted &&
            failure.Category is not FailureCategory.Validation and
            not FailureCategory.ProviderContract and
            not FailureCategory.ProviderAuthentication and
            not FailureCategory.Approval)
        {
            return new FailureDisposition.DeadLetter(FailureCodes.UnexpectedError);
        }

        return failure.Category switch
        {
            FailureCategory.Validation => new FailureDisposition.DeadLetter(failure.Code),
            FailureCategory.ProviderContract => new FailureDisposition.DeadLetter(failure.Code),
            FailureCategory.Approval => new FailureDisposition.DeadLetter(failure.Code),
            FailureCategory.NotFound => DecideNotFound(failure, context),
            FailureCategory.Dependency => new FailureDisposition.WaitForDependency(
                context.UtcNow.Add(context.EffectiveDependencyRetryDelay),
                failure.Code),
            FailureCategory.ProviderAuthentication => new FailureDisposition.NeedsAttention(failure.Code),
            FailureCategory.Unauthorized => new FailureDisposition.DeadLetter(failure.Code),
            FailureCategory.RateLimited => new FailureDisposition.RetryAt(
                context.UtcNow.Add(failure.RetryAfter ?? context.EffectiveDefaultRetryDelay),
                failure.Code),
            FailureCategory.Timeout => DecideRetryOrDeadLetter(failure, context),
            FailureCategory.Unavailable => DecideRetryOrDeadLetter(failure, context),
            FailureCategory.Conflict => DecideConflict(failure, context),
            FailureCategory.PersistenceConflict => new FailureDisposition.RetryAt(
                context.UtcNow.Add(context.EffectiveDefaultRetryDelay),
                failure.Code),
            FailureCategory.Unexpected => DecideRetryOrDeadLetter(failure, context),
            _ => new FailureDisposition.NeedsAttention(failure.Code)
        };
    }

    private static FailureDisposition DecideNotFound(IntegrationFailure failure, FailureDispositionContext context)
    {
        // Missing contractor mapping is a dependency; generic not-found of a provider resource may retry briefly then DLQ.
        if (string.Equals(failure.Code, FailureCodes.ContractorMappingMissing, StringComparison.Ordinal))
        {
            return new FailureDisposition.WaitForDependency(
                context.UtcNow.Add(context.EffectiveDependencyRetryDelay),
                failure.Code);
        }

        return DecideRetryOrDeadLetter(failure, context);
    }

    private static FailureDisposition DecideConflict(IntegrationFailure failure, FailureDispositionContext context)
    {
        if (string.Equals(failure.Code, FailureCodes.ConcurrencyConflict, StringComparison.Ordinal) ||
            string.Equals(failure.Code, FailureCodes.WorkerClaimConflict, StringComparison.Ordinal))
        {
            return new FailureDisposition.RetryAt(
                context.UtcNow.Add(context.EffectiveDefaultRetryDelay),
                failure.Code);
        }

        // Invalid transition / idempotency conflict: complete as handled only when explicitly modeled as success elsewhere.
        // As a failure, dead-letter / no blind retry.
        return new FailureDisposition.DeadLetter(failure.Code);
    }

    private static FailureDisposition DecideRetryOrDeadLetter(
        IntegrationFailure failure,
        FailureDispositionContext context)
    {
        if (context.IsExhausted)
        {
            return new FailureDisposition.DeadLetter(failure.Code);
        }

        return new FailureDisposition.RetryAt(
            context.UtcNow.Add(failure.RetryAfter ?? context.EffectiveDefaultRetryDelay),
            failure.Code);
    }
}
