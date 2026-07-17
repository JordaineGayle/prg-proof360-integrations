using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PRG.FieldFlow.Mock.Models;

namespace PRG.FieldFlow.Mock.State;

/// <summary>
/// In-memory deterministic FieldFlow mock state with fixtures and idempotency records.
/// </summary>
public sealed class MockStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ContractorDto> _contractors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkOrderDto> _workOrders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IdempotencyRecord> _idempotency = new(StringComparer.Ordinal);
    private readonly List<WebhookEventDto> _emittedWebhooks = [];
    private int _workOrderSequence = 1000;

    /// <summary>Failure injection controls.</summary>
    public FailureInjectionState Failures { get; } = new();

    /// <summary>Creates a store seeded with fictional fixtures.</summary>
    public MockStore()
    {
        Seed();
    }

    /// <summary>Resets fixtures, idempotency, webhooks, and failure injection.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _contractors.Clear();
            _workOrders.Clear();
            _idempotency.Clear();
            _emittedWebhooks.Clear();
            _workOrderSequence = 1000;
            Failures.Reset();
            Seed();
        }
    }

    /// <summary>Lists contractors.</summary>
    public IReadOnlyList<ContractorDto> ListContractors()
    {
        lock (_gate)
        {
            return _contractors.Values.Select(CloneContractor).ToArray();
        }
    }

    /// <summary>Lists work orders.</summary>
    public IReadOnlyList<WorkOrderDto> ListWorkOrders()
    {
        lock (_gate)
        {
            return _workOrders.Values.Select(CloneWorkOrder).ToArray();
        }
    }

    /// <summary>Finds a work order by id.</summary>
    public WorkOrderDto? FindWorkOrder(string workOrderId)
    {
        lock (_gate)
        {
            return _workOrders.TryGetValue(workOrderId, out var value) ? CloneWorkOrder(value) : null;
        }
    }

    /// <summary>
    /// Upserts a contractor fixture (local demo/test control). Used to resolve unknown-contractor demos.
    /// </summary>
    public ContractorDto UpsertContractor(ContractorDto contractor)
    {
        ArgumentNullException.ThrowIfNull(contractor);
        if (string.IsNullOrWhiteSpace(contractor.ContractorId))
        {
            throw new ArgumentException("contractorId is required.", nameof(contractor));
        }

        lock (_gate)
        {
            var clone = CloneContractor(contractor);
            _contractors[clone.ContractorId] = clone;
            return CloneContractor(clone);
        }
    }

    /// <summary>
    /// Upserts a work-order fixture (local demo/test control). Used for DLQ / dependency demos.
    /// </summary>
    public WorkOrderDto UpsertWorkOrder(WorkOrderDto workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        if (string.IsNullOrWhiteSpace(workOrder.WorkOrderId))
        {
            throw new ArgumentException("workOrderId is required.", nameof(workOrder));
        }

        lock (_gate)
        {
            var clone = CloneWorkOrder(workOrder);
            if (clone.EntityVersion <= 0)
            {
                clone.EntityVersion = 1;
            }

            if (string.IsNullOrWhiteSpace(clone.Status))
            {
                clone.Status = WorkOrderStatuses.Open;
            }

            _workOrders[clone.WorkOrderId] = clone;
            return CloneWorkOrder(clone);
        }
    }

    /// <summary>Finds a work order by Proof360 client reference.</summary>
    public WorkOrderDto? FindByClientReference(string clientReference)
    {
        lock (_gate)
        {
            return _workOrders.Values
                .Where(x => string.Equals(x.ClientReference, clientReference, StringComparison.Ordinal))
                .Select(CloneWorkOrder)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Creates a work order under an idempotency key.
    /// Returns existing response for repeat equivalent requests, or conflict for mismatched repeats.
    /// </summary>
    public IdempotencyResult CreateWorkOrder(string idempotencyKey, CreateWorkOrderRequest request)
    {
        lock (_gate)
        {
            var requestHash = HashRequest(request);
            if (_idempotency.TryGetValue(idempotencyKey, out var existing))
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return IdempotencyResult.Conflict();
                }

                return IdempotencyResult.Replay(CloneWorkOrder(existing.Response));
            }

            var id = $"wo-{++_workOrderSequence:D4}";
            var created = new WorkOrderDto
            {
                WorkOrderId = id,
                ContractorId = request.ContractorId,
                ClientReference = request.ClientReference,
                Status = WorkOrderStatuses.Open,
                EntityVersion = 1,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                CustomerEmail = request.CustomerEmail,
                AddressStreet = request.AddressStreet,
                AddressUnit = request.AddressUnit,
                AddressCity = request.AddressCity,
                AddressPostal = request.AddressPostal,
                ServiceType = request.ServiceType,
                Subcategory = request.Subcategory,
                WindowStart = request.WindowStart?.ToUniversalTime(),
                WindowEnd = request.WindowEnd?.ToUniversalTime(),
                Notes = request.Notes
            };

            _workOrders[id] = created;
            _idempotency[idempotencyKey] = new IdempotencyRecord(requestHash, CloneWorkOrder(created));
            return IdempotencyResult.Created(CloneWorkOrder(created));
        }
    }

    /// <summary>Patches status and entity version.</summary>
    public WorkOrderDto? PatchStatus(string workOrderId, PatchStatusRequest request)
    {
        lock (_gate)
        {
            if (!_workOrders.TryGetValue(workOrderId, out var existing))
            {
                return null;
            }

            existing.Status = request.Status!;
            existing.EntityVersion = request.EntityVersion ?? existing.EntityVersion + 1;
            return CloneWorkOrder(existing);
        }
    }

    /// <summary>Records and returns a signed webhook event payload for demo/tests.</summary>
    public WebhookEventDto BuildWebhook(
        string eventId,
        string eventType,
        WorkOrderDto workOrder,
        string providerInstanceId,
        DateTimeOffset? occurredAt = null)
    {
        var evt = new WebhookEventDto
        {
            EventId = eventId,
            EventType = eventType,
            SchemaVersion = "1.0",
            EntityVersion = workOrder.EntityVersion,
            OccurredAt = (occurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            ProviderInstanceId = providerInstanceId,
            Data = CloneWorkOrder(workOrder)
        };

        lock (_gate)
        {
            _emittedWebhooks.Add(evt);
        }

        return evt;
    }

    /// <summary>Returns previously emitted webhook events (test inspection).</summary>
    public IReadOnlyList<WebhookEventDto> EmittedWebhooks()
    {
        lock (_gate)
        {
            return _emittedWebhooks.Select(CloneWebhook).ToArray();
        }
    }

    private void Seed()
    {
        _contractors["ctr-1001"] = new ContractorDto
        {
            ContractorId = "ctr-1001",
            ComplianceId = "CMP-1001",
            Active = true,
            DisplayName = "Northwind Plumbing Fixtures Ltd",
            License = new LicenseDto { Number = "LIC-1001", ExpiresOn = "2027-12-31" },
            Insurance = new InsuranceDto { Policy = "INS-1001", ExpiresOn = "2027-06-30", Coverage = "2000000 CAD" },
            WcbNumber = "WCB-1001"
        };

        _contractors["ctr-1002"] = new ContractorDto
        {
            ContractorId = "ctr-1002",
            ComplianceId = "CMP-1002",
            Active = true,
            DisplayName = "Prairie Electric Mock Co",
            License = new LicenseDto { Number = "LIC-1002", ExpiresOn = "2026-11-15" },
            Insurance = new InsuranceDto { Policy = "INS-1002", ExpiresOn = "2026-10-01", Coverage = "1000000 CAD" },
            WcbNumber = "WCB-1002"
        };

        _workOrders["wo-2001"] = new WorkOrderDto
        {
            WorkOrderId = "wo-2001",
            ContractorId = "ctr-1001",
            ClientReference = null,
            Status = WorkOrderStatuses.Open,
            EntityVersion = 1,
            CustomerName = "Ada Fixture",
            CustomerPhone = "+1-555-0100",
            CustomerEmail = "ada.fixture@example.test",
            AddressStreet = "100 Mock Street",
            AddressCity = "Calgary",
            AddressPostal = "T2P1J9",
            ServiceType = "plumbing",
            Subcategory = "leak",
            WindowStart = DateTimeOffset.Parse("2026-08-01T15:00:00Z"),
            WindowEnd = DateTimeOffset.Parse("2026-08-01T17:00:00Z"),
            Notes = "Fictional fixture work order"
        };

        // Unknown contractor reference for dependency deferral demos.
        _workOrders["wo-2099"] = new WorkOrderDto
        {
            WorkOrderId = "wo-2099",
            ContractorId = "ctr-missing-999",
            Status = WorkOrderStatuses.Open,
            EntityVersion = 1,
            CustomerName = "Unknown Contractor Case",
            CustomerPhone = "+1-555-0199",
            AddressStreet = "99 Orphan Ave",
            AddressCity = "Edmonton",
            AddressPostal = "T5J2R7",
            ServiceType = "hvac",
            Notes = "References contractor absent from GET /contractors"
        };

        // Additive optional field fixture.
        _workOrders["wo-2100"] = new WorkOrderDto
        {
            WorkOrderId = "wo-2100",
            ContractorId = "ctr-1002",
            Status = WorkOrderStatuses.Scheduled,
            EntityVersion = 2,
            CustomerName = "Schema Evolution Fixture",
            AddressStreet = "12 Additive Rd",
            AddressCity = "Regina",
            AddressPostal = "S4P3Y2",
            ServiceType = "electrical",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["unexpectedOptionalTag"] = JsonSerializer.SerializeToElement("demo-additive-field")
            }
        };
    }

    private static string HashRequest(CreateWorkOrderRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    private static ContractorDto CloneContractor(ContractorDto source) =>
        JsonSerializer.Deserialize<ContractorDto>(JsonSerializer.Serialize(source))!;

    private static WorkOrderDto CloneWorkOrder(WorkOrderDto source) =>
        JsonSerializer.Deserialize<WorkOrderDto>(JsonSerializer.Serialize(source))!;

    private static WebhookEventDto CloneWebhook(WebhookEventDto source) =>
        JsonSerializer.Deserialize<WebhookEventDto>(JsonSerializer.Serialize(source))!;

    private sealed record IdempotencyRecord(string RequestHash, WorkOrderDto Response);
}

/// <summary>Result of an idempotent create.</summary>
public sealed class IdempotencyResult
{
    private IdempotencyResult(int statusCode, WorkOrderDto? workOrder)
    {
        StatusCode = statusCode;
        WorkOrder = workOrder;
    }

    /// <summary>HTTP status to return.</summary>
    public int StatusCode { get; }

    /// <summary>Work order body when successful.</summary>
    public WorkOrderDto? WorkOrder { get; }

    /// <summary>Created.</summary>
    public static IdempotencyResult Created(WorkOrderDto workOrder) => new(201, workOrder);

    /// <summary>Idempotent replay.</summary>
    public static IdempotencyResult Replay(WorkOrderDto workOrder) => new(200, workOrder);

    /// <summary>Conflict for mismatched key reuse.</summary>
    public static IdempotencyResult Conflict() => new(409, null);
}
