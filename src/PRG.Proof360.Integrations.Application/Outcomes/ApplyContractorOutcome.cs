namespace PRG.Proof360.Integrations.Application.Outcomes;

/// <summary>
/// Typed success outcomes for applying a contractor snapshot.
/// </summary>
public abstract record ApplyContractorOutcome
{
    private ApplyContractorOutcome()
    {
    }

    /// <summary>Vendor created.</summary>
    public sealed record Created(Guid VendorId) : ApplyContractorOutcome;

    /// <summary>Vendor updated.</summary>
    public sealed record Updated(Guid VendorId) : ApplyContractorOutcome;

    /// <summary>No mutation required.</summary>
    public sealed record NoChange(Guid VendorId) : ApplyContractorOutcome;

    /// <summary>Vendor restricted by compliance/policy.</summary>
    public sealed record Restricted(Guid VendorId) : ApplyContractorOutcome;
}
