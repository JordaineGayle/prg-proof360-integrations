using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.UnitTests.Mapping;

public sealed class ContractorToVendorMapperTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly ContractorToVendorMapper _mapper;

    public ContractorToVendorMapperTests()
    {
        _mapper = new ContractorToVendorMapper(
            new ComplianceMissingItemsCalculator(),
            new VendorApprovalPolicy(),
            _clock);
    }

    [Fact]
    public void Complete_contractor_fixture_maps_compliance_fields()
    {
        var snapshot = CompleteSnapshot();
        var vendorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var vendor = _mapper.MapNew(snapshot, vendorId);

        Assert.Equal(vendorId, vendor.VendorId);
        Assert.Equal("CMP-1001", vendor.ComplianceId);
        Assert.Equal("LIC-1001", vendor.LicenseNumber);
        Assert.Equal(new DateOnly(2027, 12, 31), vendor.LicenseExpiry);
        Assert.Equal("INS-1001", vendor.InsurancePolicy);
        Assert.Equal(new DateOnly(2027, 6, 30), vendor.InsuranceExpiry);
        Assert.Equal("2000000 CAD", vendor.InsuranceCoverage);
        Assert.Equal("WCB-1001", vendor.WcbNumber);
        Assert.Equal(VendorStatuses.PendingReview, vendor.Status);
        Assert.Null(vendor.MissingItems);
        Assert.Null(vendor.AiConfidence);
        Assert.Equal(_clock.UtcNow, vendor.CreatedAt);
    }

    [Fact]
    public void Missing_and_expired_compliance_maps_deterministically_and_safe_denies()
    {
        var snapshot = new ContractorSnapshot
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = "fieldflow-test-1",
            ExternalContractorId = "ctr-expired",
            IsActive = true,
            LicenseNumber = "LIC-OLD",
            LicenseExpiry = new DateOnly(2020, 1, 1),
            InsurancePolicy = null,
            InsuranceExpiry = null
        };

        var vendor = _mapper.MapNew(snapshot, Guid.NewGuid());

        Assert.Equal(VendorStatuses.Restricted, vendor.Status);
        Assert.Contains("insurance_policy", vendor.MissingItems!, StringComparison.Ordinal);
        Assert.Contains("wcb_number", vendor.MissingItems!, StringComparison.Ordinal);
        Assert.Contains("restrict", vendor.Rationale!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provider_active_cannot_auto_approve_on_import()
    {
        var vendor = _mapper.MapNew(CompleteSnapshot() with { IsActive = true }, Guid.NewGuid());
        Assert.Equal(VendorStatuses.PendingReview, vendor.Status);
        Assert.Null(vendor.AiConfidence);
    }

    [Fact]
    public void Created_at_is_not_overwritten_on_merge()
    {
        var existing = _mapper.MapNew(CompleteSnapshot(), Guid.NewGuid());
        var originalCreated = existing.CreatedAt;

        var merged = _mapper.MergeExisting(
            existing,
            CompleteSnapshot() with { ComplianceId = "CMP-UPDATED" },
            proof360ExplicitlyApproved: true);

        Assert.Equal(originalCreated, merged.CreatedAt);
        Assert.Equal("CMP-UPDATED", merged.ComplianceId);
    }

    private static ContractorSnapshot CompleteSnapshot() => new()
    {
        ProviderName = ProviderNames.FieldFlow,
        ProviderInstanceId = "fieldflow-test-1",
        ExternalContractorId = "ctr-1001",
        ComplianceId = "CMP-1001",
        IsActive = true,
        LicenseNumber = "LIC-1001",
        LicenseExpiry = new DateOnly(2027, 12, 31),
        InsurancePolicy = "INS-1001",
        InsuranceExpiry = new DateOnly(2027, 6, 30),
        InsuranceCoverage = "2000000 CAD",
        WcbNumber = "WCB-1001"
    };
}
