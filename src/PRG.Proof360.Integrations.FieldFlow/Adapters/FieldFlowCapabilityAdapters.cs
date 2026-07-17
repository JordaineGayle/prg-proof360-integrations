using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.FieldFlow.Adapters;

/// <summary>
/// FieldFlow contractor snapshot source.
/// </summary>
public sealed class FieldFlowContractorSnapshotSource : IContractorSnapshotSource
{
    private readonly FieldFlowClient _client;

    /// <summary>Creates the source.</summary>
    public FieldFlowContractorSnapshotSource(FieldFlowClient client) => _client = client;

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
        _client.ListContractorsAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Result<ContractorSnapshot, ProviderFailure>> GetAsync(
        string externalContractorId,
        CancellationToken cancellationToken)
    {
        var list = await _client.ListContractorsAsync(cancellationToken);
        return list.Match(
            snapshots =>
            {
                var match = snapshots.FirstOrDefault(x =>
                    string.Equals(x.ExternalContractorId, externalContractorId, StringComparison.Ordinal));
                return match is null
                    ? Result<ContractorSnapshot, ProviderFailure>.Fail(
                        new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "Contractor was not found."))
                    : Result<ContractorSnapshot, ProviderFailure>.Ok(match);
            },
            Result<ContractorSnapshot, ProviderFailure>.Fail);
    }
}

/// <summary>
/// FieldFlow work-order snapshot source.
/// </summary>
public sealed class FieldFlowWorkOrderSnapshotSource : IWorkOrderSnapshotSource
{
    private readonly FieldFlowClient _client;

    /// <summary>Creates the source.</summary>
    public FieldFlowWorkOrderSnapshotSource(FieldFlowClient client) => _client = client;

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
        _client.ListWorkOrdersAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(
        string externalWorkOrderId,
        CancellationToken cancellationToken) =>
        _client.GetWorkOrderAsync(externalWorkOrderId, cancellationToken);
}

/// <summary>
/// FieldFlow work-order dispatcher.
/// </summary>
public sealed class FieldFlowWorkOrderDispatcher : IWorkOrderDispatcher
{
    private readonly FieldFlowClient _client;

    /// <summary>Creates the dispatcher.</summary>
    public FieldFlowWorkOrderDispatcher(FieldFlowClient client) => _client = client;

    /// <inheritdoc />
    public Task<Result<WorkOrderSnapshot, ProviderFailure>> DispatchAsync(
        DispatchWorkOrderCommand command,
        CancellationToken cancellationToken) =>
        _client.CreateWorkOrderAsync(command, cancellationToken);
}

/// <summary>
/// FieldFlow reconciler. Client-reference lookup scans the list endpoint in Phase 1.
/// </summary>
public sealed class FieldFlowWorkOrderReconciler : IWorkOrderReconciler
{
    private readonly FieldFlowClient _client;

    /// <summary>Creates the reconciler.</summary>
    public FieldFlowWorkOrderReconciler(FieldFlowClient client) => _client = client;

    /// <inheritdoc />
    public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByExternalIdAsync(
        string externalWorkOrderId,
        CancellationToken cancellationToken) =>
        _client.GetWorkOrderAsync(externalWorkOrderId, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByClientReferenceAsync(
        string clientReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientReference))
        {
            return Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                ProviderFailure.Validation("client_reference_required", "clientReference is required."));
        }

        var list = await _client.ListWorkOrdersAsync(cancellationToken);
        return list.Match(
            snapshots =>
            {
                var match = snapshots.FirstOrDefault(x =>
                    string.Equals(x.ClientReference, clientReference, StringComparison.Ordinal));
                return match is null
                    ? Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                        new ProviderFailure(
                            ProviderFailureKind.NotFound,
                            "not_found",
                            "No work order matched the client reference."))
                    : Result<WorkOrderSnapshot, ProviderFailure>.Ok(match);
            },
            Result<WorkOrderSnapshot, ProviderFailure>.Fail);
    }
}
