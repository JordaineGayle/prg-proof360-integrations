using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.Demo;
using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Health;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Application.Observability;
using PRG.Proof360.Integrations.Application.Replay;
using PRG.Proof360.Integrations.Application.WorkOrders;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.Application.DependencyInjection;

/// <summary>
/// Registers Application services (mappers, inbound/outbound use cases, disposition).
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds Application mapping policies and inbound/outbound handlers.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<InboundSyncOptions>(configuration.GetSection(InboundSyncOptions.SectionName));
        services.Configure<OutboundDispatchOptions>(configuration.GetSection(OutboundDispatchOptions.SectionName));
        services.Configure<ConnectorHealthOptions>(configuration.GetSection(ConnectorHealthOptions.SectionName));
        services.Configure<AdminReplayOptions>(configuration.GetSection(AdminReplayOptions.SectionName));
        RegisterCore(services);
        return services;
    }

    /// <summary>
    /// Adds Application services with empty inbound/outbound options (tests).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<InboundSyncOptions>(_ => { });
        services.Configure<OutboundDispatchOptions>(_ => { });
        services.Configure<ConnectorHealthOptions>(_ => { });
        services.Configure<AdminReplayOptions>(_ => { });
        RegisterCore(services);
        return services;
    }

    private static void RegisterCore(IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<VendorApprovalPolicy>();
        services.AddSingleton<JobStatusTransitionPolicy>();
        services.AddSingleton<ComplianceMissingItemsCalculator>();
        services.AddSingleton<WorkOrderStatusMappingPolicy>();
        services.AddSingleton<ContractorToVendorMapper>();
        services.AddSingleton<WorkOrderToJobMapper>();
        services.AddSingleton<FailureDispositionPolicy>();
        services.AddSingleton<ConnectorHealthStatusPolicy>();
        services.AddScoped<StructuredAuditWriter>();

        services.AddScoped<ReceiveProviderEventHandler>();
        services.AddScoped<ReceiveWebhookEventHandler>();
        services.AddScoped<ApplyContractorSnapshotHandler>();
        services.AddScoped<ApplyWorkOrderSnapshotHandler>();
        services.AddScoped<ProcessInboxMessageHandler>();
        services.AddScoped<ImportContractorsHandler>();
        services.AddScoped<ImportWorkOrdersHandler>();
        services.AddScoped<QueueJobDispatchHandler>();
        services.AddScoped<ProcessOutboxMessageHandler>();
        services.AddScoped<SeedQualifiedDispatchDemoHandler>();
        services.AddScoped<GetDemoSummaryHandler>();
        services.AddScoped<NudgeWaitingDependenciesHandler>();
        services.AddScoped<GetConnectorHealthHandler>();
        services.AddScoped<ReplayDeadLetterHandler>();
    }
}
