namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Stable machine-readable failure codes. Never match on exception message text.
/// </summary>
public static class FailureCodes
{
    /// <summary>Generic validation failure.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>Required field missing.</summary>
    public const string RequiredFieldMissing = "required_field_missing";

    /// <summary>Invalid timestamp/date.</summary>
    public const string InvalidTimestamp = "invalid_timestamp";

    /// <summary>Job not found.</summary>
    public const string JobNotFound = "job_not_found";

    /// <summary>Vendor not found.</summary>
    public const string VendorNotFound = "vendor_not_found";

    /// <summary>Identity link not found.</summary>
    public const string IdentityNotFound = "identity_not_found";

    /// <summary>Invalid status transition.</summary>
    public const string InvalidStatusTransition = "invalid_status_transition";

    /// <summary>Idempotency key conflict.</summary>
    public const string IdempotencyKeyConflict = "idempotency_key_conflict";

    /// <summary>Optimistic concurrency conflict.</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";

    /// <summary>Vendor not approved.</summary>
    public const string VendorNotApproved = "vendor_not_approved";

    /// <summary>Job not qualified for dispatch.</summary>
    public const string JobNotQualified = "job_not_qualified";

    /// <summary>Approval required.</summary>
    public const string ApprovalRequired = "approval_required";

    /// <summary>Contractor mapping missing.</summary>
    public const string ContractorMappingMissing = "contractor_mapping_missing";

    /// <summary>Unsupported provider schema.</summary>
    public const string UnsupportedSchema = "unsupported_schema";

    /// <summary>Unknown provider status.</summary>
    public const string UnknownProviderStatus = "unknown_provider_status";

    /// <summary>Malformed provider payload.</summary>
    public const string MalformedProviderPayload = "malformed_provider_payload";

    /// <summary>Provider authentication failed.</summary>
    public const string ProviderAuthenticationFailed = "provider_authentication_failed";

    /// <summary>Provider forbidden.</summary>
    public const string ProviderForbidden = "provider_forbidden";

    /// <summary>Provider rate limited.</summary>
    public const string ProviderRateLimited = "provider_rate_limited";

    /// <summary>Provider timeout.</summary>
    public const string ProviderTimeout = "provider_timeout";

    /// <summary>Provider unavailable.</summary>
    public const string ProviderUnavailable = "provider_unavailable";

    /// <summary>Circuit open.</summary>
    public const string CircuitOpen = "circuit_open";

    /// <summary>Duplicate external identity.</summary>
    public const string DuplicateExternalIdentity = "duplicate_external_identity";

    /// <summary>Worker claim conflict.</summary>
    public const string WorkerClaimConflict = "worker_claim_conflict";

    /// <summary>Unexpected sanitized error.</summary>
    public const string UnexpectedError = "unexpected_error";

    /// <summary>Unsupported provider capability.</summary>
    public const string UnsupportedCapability = "unsupported_capability";

    /// <summary>Ambiguous provider write.</summary>
    public const string AmbiguousProviderWrite = "ambiguous_provider_write";

    /// <summary>Provider resource not found.</summary>
    public const string ProviderNotFound = "provider_not_found";

    /// <summary>Provider conflict.</summary>
    public const string ProviderConflict = "provider_conflict";

    /// <summary>Webhook signature invalid or missing.</summary>
    public const string WebhookSignatureInvalid = "webhook_signature_invalid";

    /// <summary>Webhook timestamp outside replay window.</summary>
    public const string WebhookTimestampSkew = "webhook_timestamp_skew";

    /// <summary>Webhook request body exceeds size limit.</summary>
    public const string WebhookPayloadTooLarge = "webhook_payload_too_large";

    /// <summary>Equal entity version with conflicting payload hash.</summary>
    public const string VersionPayloadConflict = "version_payload_conflict";

    /// <summary>Unsupported webhook event type.</summary>
    public const string UnsupportedEventType = "unsupported_event_type";
}
