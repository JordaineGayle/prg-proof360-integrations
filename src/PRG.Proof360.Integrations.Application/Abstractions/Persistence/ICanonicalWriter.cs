using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Write/read port for Proof360 canonical entities. Updates occur on tracked entities via the unit of work.
/// </summary>
public interface ICanonicalWriter
{
    /// <summary>Stages a new Vendor.</summary>
    Task AddVendorAsync(Vendor vendor, CancellationToken cancellationToken = default);

    /// <summary>Stages a new Job.</summary>
    Task AddJobAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>Stages a new Transcript.</summary>
    Task AddTranscriptAsync(Transcript transcript, CancellationToken cancellationToken = default);

    /// <summary>Loads a Vendor by id.</summary>
    Task<Vendor?> FindVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);

    /// <summary>Loads a Job by id.</summary>
    Task<Job?> FindJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Counts Vendors (tests/diagnostics).</summary>
    Task<int> CountVendorsAsync(CancellationToken cancellationToken = default);

    /// <summary>Counts Jobs (tests/diagnostics).</summary>
    Task<int> CountJobsAsync(CancellationToken cancellationToken = default);
}
