using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Application.Demo;
using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.FieldFlow.Resilience;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Local/demo-only endpoints. Registered only in Development.
/// </summary>
public static class DemoSeedEndpoints
{
    /// <summary>Maps demo seed routes.</summary>
    public static IEndpointRouteBuilder MapDemoSeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/_demo/reset-local-state", async (
            ConnectorDbContext db,
            FieldFlowResilienceState resilience,
            CancellationToken cancellationToken) =>
        {
            // Clear durable demo state left by prior scenario runs (DLQ, backlog → Degraded).
            var deleted = new
            {
                audit = await db.AuditEvents.ExecuteDeleteAsync(cancellationToken),
                outbox = await db.OutboxMessages.ExecuteDeleteAsync(cancellationToken),
                inbox = await db.InboxMessages.ExecuteDeleteAsync(cancellationToken),
                identity = await db.ProviderIdentityLinks.ExecuteDeleteAsync(cancellationToken),
                transcripts = await db.Transcripts.ExecuteDeleteAsync(cancellationToken),
                jobs = await db.Jobs.ExecuteDeleteAsync(cancellationToken),
                vendors = await db.Vendors.ExecuteDeleteAsync(cancellationToken),
                connectorState = await db.ConnectorStates.ExecuteDeleteAsync(cancellationToken)
            };

            resilience.Reset();

            return Results.Ok(new { reset = true, deleted });
        })
        .WithTags("Demo")
        .WithSummary("Clear local SQLite connector state + process resilience projection");

        endpoints.MapPost("/_demo/seed-qualified-dispatch", async (

            SeedQualifiedDispatchDemoHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.ToHttpResult(http, outcome => Results.Ok(new
            {
                jobId = outcome.JobId,
                vendorId = outcome.VendorId,
                created = outcome.Created,
                dispatch = $"/connectors/fieldflow/jobs/{outcome.JobId}/dispatch"
            }));
        })
        .WithTags("Demo")
        .WithSummary("Seed a qualified Job ready for outbound dispatch");

        endpoints.MapGet("/_demo/summary", async (
            GetDemoSummaryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var summary = await handler.HandleAsync(cancellationToken);
            return Results.Ok(summary);
        })
        .WithTags("Demo")
        .WithSummary("Sanitized local demo counts (vendors, jobs, inbox states)");

        endpoints.MapPost("/_demo/nudge-waiting-dependencies", async (
            NudgeWaitingDependenciesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var (madeDue, processed) = await handler.HandleAsync(maxBatch: 20, cancellationToken);
            return Results.Ok(new { madeDue, processed });
        })
        .WithTags("Demo")
        .WithSummary("Make waiting-dependency inbox messages due and process a bounded batch");

        endpoints.MapPost("/_demo/exhaust-waiting-dependencies", async (
            ExhaustWaitingDependenciesDemoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var (prepared, processed, deadLettered) = await handler.HandleAsync(
                maxAttempts: 8,
                maxBatch: 20,
                cancellationToken);
            return Results.Ok(new { prepared, processed, deadLettered });
        })
        .WithTags("Demo")
        .WithSummary("Exhaust waiting-dependency messages into the dead-letter queue");

        endpoints.MapPost("/_demo/seed-unapproved-dispatch", async (
            SeedUnapprovedDispatchDemoHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.ToHttpResult(http, outcome => Results.Ok(new
            {
                jobId = outcome.JobId,
                vendorId = outcome.VendorId,
                created = outcome.Created,
                dispatch = $"/connectors/fieldflow/jobs/{outcome.JobId}/dispatch"
            }));
        })
        .WithTags("Demo")
        .WithSummary("Seed a qualified Job with a non-approved Vendor (approval gate)");

        endpoints.MapPost("/_demo/seed-ambiguous-dispatch", async (
            SeedAmbiguousDispatchDemoHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.ToHttpResult(http, outcome => Results.Ok(new
            {
                jobId = outcome.JobId,
                vendorId = outcome.VendorId,
                created = outcome.Created,
                dispatch = $"/connectors/fieldflow/jobs/{outcome.JobId}/dispatch"
            }));
        })
        .WithTags("Demo")
        .WithSummary("Seed a qualified Job for ambiguous-POST reconciliation demos");

        return endpoints;
    }
}
