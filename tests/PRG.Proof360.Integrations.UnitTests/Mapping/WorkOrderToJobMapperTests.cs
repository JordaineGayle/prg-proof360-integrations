using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.UnitTests.Mapping;

public sealed class WorkOrderToJobMapperTests
{
    private readonly WorkOrderToJobMapper _mapper = new(
        new WorkOrderStatusMappingPolicy(),
        new JobStatusTransitionPolicy());

    [Fact]
    public void Complete_work_order_fixture_maps_customer_address_and_status()
    {
        var jobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var vendorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var result = _mapper.MapNewFromProvider(CompleteSnapshot(), jobId, vendorId);

        Assert.True(result.IsSuccess);
        var job = result.Job!;
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(JobSources.FieldFlow, job.Source);
        Assert.Equal("Ada Fixture", job.CustomerName);
        Assert.Equal("Calgary", job.AddressCity);
        Assert.Equal("plumbing", job.ServiceType);
        Assert.Equal(JobStatuses.Dispatched, job.Status);
        Assert.Equal(vendorId, job.AssignedVendorId);
        Assert.Null(job.AiConfidence);
        Assert.Null(job.AiJson);
        Assert.Null(job.Priority);
    }

    [Fact]
    public void FieldFlow_originated_job_initializes_permitted_fields()
    {
        var result = _mapper.MapNewFromProvider(CompleteSnapshot(), Guid.NewGuid(), assignedVendorId: null);
        Assert.True(result.IsSuccess);
        Assert.Equal(JobSources.FieldFlow, result.Job!.Source);
        Assert.Equal("100 Mock Street", result.Job.AddressStreet);
        Assert.Equal("leak", result.Job.Subcategory);
        Assert.False(result.Job.ComplianceOnly);
    }

    [Fact]
    public void Proof360_ownership_prevents_provider_overwrite()
    {
        var existing = new Job
        {
            JobId = Guid.NewGuid(),
            Source = JobSources.Proof360,
            CustomerName = "Proof360 Customer",
            AddressCity = "Toronto",
            ServiceType = "hvac",
            Priority = "high",
            WindowStart = DateTimeOffset.Parse("2026-09-01T15:00:00Z"),
            WindowEnd = DateTimeOffset.Parse("2026-09-01T17:00:00Z"),
            NotesScope = "Proof360 scope",
            ComplianceOnly = true,
            Status = JobStatuses.Dispatched
        };

        var provider = CompleteSnapshot() with
        {
            ProviderStatus = "scheduled",
            CustomerName = "Provider Echo",
            AddressCity = "Calgary",
            ServiceType = "plumbing",
            Notes = "provider notes",
            WindowStart = DateTimeOffset.Parse("2026-10-01T15:00:00Z"),
            WindowEnd = DateTimeOffset.Parse("2026-10-01T17:00:00Z")
        };

        var result = _mapper.MergeUpdate(existing, provider, assignedVendorId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Proof360 Customer", result.Job!.CustomerName);
        Assert.Equal("Toronto", result.Job.AddressCity);
        Assert.Equal("hvac", result.Job.ServiceType);
        Assert.Equal("high", result.Job.Priority);
        Assert.Equal("Proof360 scope", result.Job.NotesScope);
        Assert.True(result.Job.ComplianceOnly);
        Assert.Equal(JobStatuses.Scheduled, result.Job.Status);
        Assert.Contains("customer_name", result.IgnoredOwnershipFields);
        Assert.Contains("window_start", result.IgnoredOwnershipFields);
    }

    [Fact]
    public void No_ai_fields_are_fabricated()
    {
        var result = _mapper.MapNewFromProvider(CompleteSnapshot(), Guid.NewGuid(), null);
        Assert.Null(result.Job!.AiConfidence);
        Assert.Null(result.Job.AiJson);
    }

    private static WorkOrderSnapshot CompleteSnapshot() => new()
    {
        ProviderName = ProviderNames.FieldFlow,
        ProviderInstanceId = "fieldflow-test-1",
        ExternalWorkOrderId = "wo-2001",
        ExternalContractorId = "ctr-1001",
        ProviderStatus = "open",
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
}
