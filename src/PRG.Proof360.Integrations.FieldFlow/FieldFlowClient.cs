using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using PRG.Proof360.Integrations.Core.Observability;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow.Contracts;
using PRG.Proof360.Integrations.FieldFlow.Mapping;
using PRG.Proof360.Integrations.FieldFlow.Resilience;

namespace PRG.Proof360.Integrations.FieldFlow;

/// <summary>
/// Typed FieldFlow HTTP client. Converts expected HTTP outcomes to <see cref="ProviderFailure"/> once.
/// Per-call retries/timeouts/circuit are owned exclusively by the FieldFlow resilience pipeline.
/// </summary>
public sealed class FieldFlowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FieldFlowOptions _options;
    private readonly FieldFlowContractorMapper _contractorMapper;
    private readonly FieldFlowWorkOrderMapper _workOrderMapper;
    private readonly FieldFlowResilienceState _resilienceState;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the client.</summary>
    public FieldFlowClient(
        HttpClient httpClient,
        IOptions<FieldFlowOptions> options,
        FieldFlowContractorMapper contractorMapper,
        FieldFlowWorkOrderMapper workOrderMapper,
        FieldFlowResilienceState resilienceState,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _contractorMapper = contractorMapper;
        _workOrderMapper = workOrderMapper;
        _resilienceState = resilienceState;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Configured provider instance id.</summary>
    public string ProviderInstanceId => _options.ProviderInstanceId;

    /// <summary>Lists contractors.</summary>
    public async Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListContractorsAsync(
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.GetAsync(new Uri("contractors", UriKind.Relative), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failure = await ClassifyAsync(response, cancellationToken);
                RecordProvider("list_contractors", failure.Kind, sw.Elapsed.TotalMilliseconds);
                return FailListContractors(failure);
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<FieldFlowContractorDto>>(JsonOptions, cancellationToken)
                       ?? [];
            var snapshots = new List<ContractorSnapshot>(dtos.Count);
            foreach (var dto in dtos)
            {
                var mapped = _contractorMapper.ToSnapshot(dto, _options.ProviderInstanceId);
                if (mapped is Result<ContractorSnapshot, ProviderFailure>.Failed failed)
                {
                    return FailListContractors(failed.Error);
                }

                snapshots.Add(((Result<ContractorSnapshot, ProviderFailure>.Succeeded)mapped).Value);
            }

            ConnectorTelemetry.RecordProviderRequest("list_contractors", "success", sw.Elapsed.TotalMilliseconds);
            return Ok(Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Ok(snapshots));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderTransport(ex))
        {
            var failure = MapTransport(ex, ambiguousWrite: false);
            RecordProvider("list_contractors", failure.Kind, sw.Elapsed.TotalMilliseconds);
            return FailListContractors(failure);
        }
    }

    /// <summary>Lists work orders.</summary>
    public async Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListWorkOrdersAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(new Uri("work-orders", UriKind.Relative), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return FailListWorkOrders(await ClassifyAsync(response, cancellationToken));
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<FieldFlowWorkOrderDto>>(JsonOptions, cancellationToken)
                       ?? [];
            var snapshots = new List<WorkOrderSnapshot>(dtos.Count);
            foreach (var dto in dtos)
            {
                var mapped = _workOrderMapper.ToSnapshot(dto, _options.ProviderInstanceId);
                if (mapped is Result<WorkOrderSnapshot, ProviderFailure>.Failed failed)
                {
                    return FailListWorkOrders(failed.Error);
                }

                snapshots.Add(((Result<WorkOrderSnapshot, ProviderFailure>.Succeeded)mapped).Value);
            }

            return Ok(Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Ok(snapshots));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderTransport(ex))
        {
            return FailListWorkOrders(MapTransport(ex, ambiguousWrite: false));
        }
    }

    /// <summary>Gets a work order by id.</summary>
    public async Task<Result<WorkOrderSnapshot, ProviderFailure>> GetWorkOrderAsync(
        string externalWorkOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalWorkOrderId))
        {
            return Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                ProviderFailure.Validation("missing_work_order_id", "workOrderId is required."));
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                new Uri($"work-orders/{Uri.EscapeDataString(externalWorkOrderId)}", UriKind.Relative),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return FailWorkOrder(await ClassifyAsync(response, cancellationToken));
            }

            var dto = await response.Content.ReadFromJsonAsync<FieldFlowWorkOrderDto>(JsonOptions, cancellationToken);
            if (dto is null)
            {
                return FailWorkOrder(ProviderFailure.Validation("invalid_body", "Work order response body was empty."));
            }

            return Ok(_workOrderMapper.ToSnapshot(dto, _options.ProviderInstanceId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderTransport(ex))
        {
            return FailWorkOrder(MapTransport(ex, ambiguousWrite: false));
        }
    }

    /// <summary>Creates a work order.</summary>
    public async Task<Result<WorkOrderSnapshot, ProviderFailure>> CreateWorkOrderAsync(
        DispatchWorkOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("work-orders", UriKind.Relative))
            {
                Content = JsonContent.Create(_workOrderMapper.ToCreateRequest(command), options: JsonOptions)
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", command.IdempotencyKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return FailWorkOrder(await ClassifyAsync(response, cancellationToken));
            }

            var dto = await response.Content.ReadFromJsonAsync<FieldFlowWorkOrderDto>(JsonOptions, cancellationToken);
            if (dto is null)
            {
                return FailWorkOrder(ProviderFailure.Validation("invalid_body", "Create work order response body was empty."));
            }

            return Ok(_workOrderMapper.ToSnapshot(dto, _options.ProviderInstanceId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokenCircuitException ex)
        {
            return FailWorkOrder(MapTransport(ex, ambiguousWrite: false));
        }
        catch (TimeoutRejectedException ex)
        {
            return FailWorkOrder(MapTransport(ex, ambiguousWrite: true));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or OperationCanceledException)
        {
            // Ambiguous when a write may have been accepted before the response was lost.
            return FailWorkOrder(MapTransport(ex, ambiguousWrite: true));
        }
    }

    private Result<T, ProviderFailure> Ok<T>(Result<T, ProviderFailure> result)
        where T : notnull
    {
        if (result is Result<T, ProviderFailure>.Succeeded)
        {
            _resilienceState.RecordSuccess(_timeProvider.GetUtcNow());
        }
        else if (result is Result<T, ProviderFailure>.Failed failed)
        {
            TrackFailure(failed.Error);
        }

        return result;
    }

    private Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure> FailListContractors(ProviderFailure failure)
    {
        TrackFailure(failure);
        return Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Fail(failure);
    }

    private Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure> FailListWorkOrders(ProviderFailure failure)
    {
        TrackFailure(failure);
        return Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Fail(failure);
    }

    private Result<WorkOrderSnapshot, ProviderFailure> FailWorkOrder(ProviderFailure failure)
    {
        TrackFailure(failure);
        return Result<WorkOrderSnapshot, ProviderFailure>.Fail(failure);
    }

    private void TrackFailure(ProviderFailure failure)
    {
        var needsAttention = failure.Kind is ProviderFailureKind.Authentication or ProviderFailureKind.Forbidden;
        _resilienceState.RecordFailure(failure.Kind.ToString(), failure.Code, _timeProvider.GetUtcNow(), needsAttention);
        if (failure.Kind == ProviderFailureKind.RateLimited)
        {
            ConnectorTelemetry.RateLimits.Add(1);
        }
    }

    private static void RecordProvider(string operation, ProviderFailureKind kind, double durationMs)
    {
        var outcome = kind switch
        {
            ProviderFailureKind.Timeout => "timeout",
            ProviderFailureKind.RateLimited => "rate_limited",
            ProviderFailureKind.CircuitOpen => "circuit_open",
            ProviderFailureKind.Authentication or ProviderFailureKind.Forbidden => "auth",
            ProviderFailureKind.Validation => "validation",
            _ => "failure"
        };
        ConnectorTelemetry.RecordProviderRequest(operation, outcome, durationMs);
    }

    private static bool IsProviderTransport(Exception ex) =>
        ex is BrokenCircuitException
            or TimeoutRejectedException
            or HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or OperationCanceledException;

    private static ProviderFailure MapTransport(Exception exception, bool ambiguousWrite)
    {
        if (exception is BrokenCircuitException)
        {
            return new ProviderFailure(
                ProviderFailureKind.CircuitOpen,
                "circuit_open",
                "FieldFlow circuit is open; call was not attempted.");
        }

        if (exception is TimeoutRejectedException)
        {
            return new ProviderFailure(ProviderFailureKind.Timeout, "timeout", "FieldFlow request timed out.");
        }

        if (ambiguousWrite)
        {
            return new ProviderFailure(
                ProviderFailureKind.AmbiguousWrite,
                "ambiguous_write",
                "FieldFlow create may have succeeded but the response was not observed.");
        }

        return FieldFlowErrorClassifier.FromTransportException(exception);
    }

    private static async Task<ProviderFailure> ClassifyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        TimeSpan? retryAfter = null;
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            retryAfter = delta;
        }
        else if (response.Headers.TryGetValues("Retry-After", out var values) &&
                 int.TryParse(values.FirstOrDefault(), out var seconds))
        {
            retryAfter = TimeSpan.FromSeconds(seconds);
        }

        return FieldFlowErrorClassifier.Classify(response.StatusCode, body, retryAfter);
    }

    /// <summary>Configures the named HttpClient defaults from options.</summary>
    public static void ConfigureHttpClient(HttpClient client, FieldFlowOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
        // Polly owns per-attempt and retry budgets; keep HttpClient.Timeout as a coarse ceiling.
        client.Timeout = TimeSpan.FromMilliseconds(Math.Max(options.TimeoutMilliseconds, 60_000));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
}
