using System.Text.Json;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow.Contracts;
using PRG.Proof360.Integrations.FieldFlow.Mapping;

namespace PRG.Proof360.Integrations.UnitTests.FieldFlow;

public sealed class FieldFlowMapperValidationTests
{
    private readonly FieldFlowContractorMapper _contractors = new();
    private readonly FieldFlowWorkOrderMapper _workOrders = new();

    [Fact]
    public void Unknown_optional_field_is_compatible_and_observable()
    {
        var json = """
            {
              "workOrderId": "wo-2100",
              "contractorId": "ctr-1002",
              "status": "scheduled",
              "entityVersion": 2,
              "customerName": "Schema Evolution Fixture",
              "addressStreet": "12 Additive Rd",
              "addressCity": "Regina",
              "serviceType": "electrical",
              "unexpectedOptionalTag": "demo-additive-field"
            }
            """;

        var dto = JsonSerializer.Deserialize<FieldFlowWorkOrderDto>(json)!;
        var result = _workOrders.ToSnapshot(dto, "fieldflow-test-1", schemaVersion: "1.0");

        Assert.True(result.IsSuccess);
        var snapshot = result.Match(v => v, _ => throw new InvalidOperationException());
        Assert.Contains("unexpectedOptionalTag", snapshot.UnknownOptionalFields);
        Assert.Equal("scheduled", snapshot.ProviderStatus);
        Assert.Equal(2, snapshot.EntityVersion);
        Assert.Equal("1.0", snapshot.SchemaVersion);
    }

    [Fact]
    public void Missing_required_id_fails_with_validation_classification()
    {
        var contractor = _contractors.ToSnapshot(new FieldFlowContractorDto { Active = true }, "fieldflow-test-1");
        Assert.True(contractor.IsFailure);
        var contractorError = ((Result<ContractorSnapshot, ProviderFailure>.Failed)contractor).Error;
        Assert.Equal(ProviderFailureKind.Validation, contractorError.Kind);
        Assert.Equal("missing_contractor_id", contractorError.Code);

        var workOrder = _workOrders.ToSnapshot(
            new FieldFlowWorkOrderDto { Status = "open" },
            "fieldflow-test-1");
        Assert.True(workOrder.IsFailure);
        var workOrderError = ((Result<WorkOrderSnapshot, ProviderFailure>.Failed)workOrder).Error;
        Assert.Equal(ProviderFailureKind.Validation, workOrderError.Kind);
        Assert.Equal("missing_work_order_id", workOrderError.Code);
    }

    [Fact]
    public void Invalid_license_date_fails_with_validation_classification()
    {
        var result = _contractors.ToSnapshot(
            new FieldFlowContractorDto
            {
                ContractorId = "ctr-1",
                License = new FieldFlowLicenseDto { Number = "L1", ExpiresOn = "not-a-date" }
            },
            "fieldflow-test-1");

        Assert.True(result.IsFailure);
        var error = ((Result<ContractorSnapshot, ProviderFailure>.Failed)result).Error;
        Assert.Equal(ProviderFailureKind.Validation, error.Kind);
        Assert.Equal("invalid_date", error.Code);
    }

    [Fact]
    public void Complete_contractor_dto_maps_to_snapshot()
    {
        var result = _contractors.ToSnapshot(
            new FieldFlowContractorDto
            {
                ContractorId = "ctr-1001",
                ComplianceId = "CMP-1001",
                Active = true,
                DisplayName = "Ignored",
                License = new FieldFlowLicenseDto { Number = "LIC-1001", ExpiresOn = "2027-12-31" },
                Insurance = new FieldFlowInsuranceDto
                {
                    Policy = "INS-1001",
                    ExpiresOn = "2027-06-30",
                    Coverage = "2000000 CAD"
                },
                WcbNumber = "WCB-1001"
            },
            "fieldflow-test-1");

        Assert.True(result.IsSuccess);
        var snapshot = result.Match(v => v, _ => throw new InvalidOperationException());
        Assert.Equal("ctr-1001", snapshot.ExternalContractorId);
        Assert.Equal(new DateOnly(2027, 12, 31), snapshot.LicenseExpiry);
    }
}
