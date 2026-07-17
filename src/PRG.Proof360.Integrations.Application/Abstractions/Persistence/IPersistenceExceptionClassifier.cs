namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Classifies persistence exceptions into expected idempotency/concurrency outcomes.
/// </summary>
public interface IPersistenceExceptionClassifier
{
    /// <summary>True when the exception is a unique constraint / unique index violation.</summary>
    bool IsUniqueConstraintViolation(Exception exception);

    /// <summary>True when the exception is an optimistic concurrency conflict.</summary>
    bool IsConcurrencyConflict(Exception exception);
}
