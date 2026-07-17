using Microsoft.EntityFrameworkCore;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// SQLite/EF exception classifier for expected unique and concurrency outcomes.
/// </summary>
internal sealed class PersistenceExceptionClassifier : Application.Abstractions.Persistence.IPersistenceExceptionClassifier
{
    /// <inheritdoc />
    public bool IsUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique index", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ux_inbox_event", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ux_identity_external", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return exception is DbUpdateException dbUpdate &&
               dbUpdate.InnerException is not null &&
               IsUniqueConstraintViolation(dbUpdate.InnerException);
    }

    /// <inheritdoc />
    public bool IsConcurrencyConflict(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception.InnerException is DbUpdateConcurrencyException;
}
