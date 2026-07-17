using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Use-case oriented persistence for canonical entities without leaking EF types.
/// </summary>
public interface ICanonicalWriter
{
    /// <summary>Stages a Vendor insert.</summary>
    Task AddVendorAsync(Vendor vendor, CancellationToken cancellationToken = default);

    /// <summary>Stages a Job insert.</summary>
    Task AddJobAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>Stages a Transcript insert.</summary>
    Task AddTranscriptAsync(Transcript transcript, CancellationToken cancellationToken = default);

    /// <summary>Loads a Vendor by internal identifier.</summary>
    Task<Vendor?> FindVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);

    /// <summary>Loads a Job by internal identifier.</summary>
    Task<Job?> FindJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
