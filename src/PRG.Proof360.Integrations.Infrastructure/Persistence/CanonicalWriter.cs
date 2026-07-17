using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// EF-backed canonical writer.
/// </summary>
internal sealed class CanonicalWriter : ICanonicalWriter
{
    private readonly ConnectorDbContext _dbContext;

    public CanonicalWriter(ConnectorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task AddVendorAsync(Vendor vendor, CancellationToken cancellationToken = default)
        => _dbContext.Vendors.AddAsync(vendor, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task AddJobAsync(Job job, CancellationToken cancellationToken = default)
        => _dbContext.Jobs.AddAsync(job, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task AddTranscriptAsync(Transcript transcript, CancellationToken cancellationToken = default)
        => _dbContext.Transcripts.AddAsync(transcript, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task<Vendor?> FindVendorAsync(Guid vendorId, CancellationToken cancellationToken = default)
        => _dbContext.Vendors.SingleOrDefaultAsync(x => x.VendorId == vendorId, cancellationToken);

    /// <inheritdoc />
    public Task<Job?> FindJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _dbContext.Jobs.SingleOrDefaultAsync(x => x.JobId == jobId, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountVendorsAsync(CancellationToken cancellationToken = default)
        => _dbContext.Vendors.CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountJobsAsync(CancellationToken cancellationToken = default)
        => _dbContext.Jobs.CountAsync(cancellationToken);
}
