using System.Diagnostics;

namespace PRG.Proof360.Integrations.Core.Results;

/// <summary>
/// Discriminated success/failure result for expected outcomes at domain, adapter, and application boundaries.
/// Unexpected programmer defects and infrastructure corruption remain exceptions.
/// </summary>
/// <typeparam name="TSuccess">Non-null success payload.</typeparam>
/// <typeparam name="TFailure">Non-null failure payload.</typeparam>
public abstract record Result<TSuccess, TFailure>
    where TSuccess : notnull
    where TFailure : notnull
{
    private Result()
    {
    }

    /// <summary>Successful outcome.</summary>
    /// <param name="Value">Success payload.</param>
    public sealed record Succeeded(TSuccess Value) : Result<TSuccess, TFailure>;

    /// <summary>Failed outcome.</summary>
    /// <param name="Error">Failure payload.</param>
    public sealed record Failed(TFailure Error) : Result<TSuccess, TFailure>;

    /// <summary>True when this is a <see cref="Succeeded"/>.</summary>
    public bool IsSuccess => this is Succeeded;

    /// <summary>True when this is a <see cref="Failed"/>.</summary>
    public bool IsFailure => this is Failed;

    /// <summary>Exhaustive match over success and failure.</summary>
    public TResult Match<TResult>(
        Func<TSuccess, TResult> onSuccess,
        Func<TFailure, TResult> onFailure) =>
        this switch
        {
            Succeeded success => onSuccess(success.Value),
            Failed failure => onFailure(failure.Error),
            _ => throw new UnreachableException()
        };

    /// <summary>Maps a success value; failures pass through unchanged.</summary>
    public Result<TNext, TFailure> Map<TNext>(Func<TSuccess, TNext> map)
        where TNext : notnull =>
        Match(
            value => Result<TNext, TFailure>.Ok(map(value)),
            Result<TNext, TFailure>.Fail);

    /// <summary>Binds a success value into another result; failures pass through unchanged.</summary>
    public Result<TNext, TFailure> Bind<TNext>(Func<TSuccess, Result<TNext, TFailure>> bind)
        where TNext : notnull =>
        Match(bind, Result<TNext, TFailure>.Fail);

    /// <summary>Creates a success result.</summary>
    public static Result<TSuccess, TFailure> Ok(TSuccess value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Succeeded(value);
    }

    /// <summary>Creates a failure result.</summary>
    public static Result<TSuccess, TFailure> Fail(TFailure error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failed(error);
    }
}
