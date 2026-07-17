using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Health;
using PRG.Proof360.Integrations.FieldFlow.Adapters;
using PRG.Proof360.Integrations.FieldFlow.Mapping;
using PRG.Proof360.Integrations.FieldFlow.Resilience;
using PRG.Proof360.Integrations.FieldFlow.Security;

namespace PRG.Proof360.Integrations.FieldFlow.DependencyInjection;

/// <summary>
/// Registers the FieldFlow anti-corruption layer and capability ports.
/// </summary>
public static class FieldFlowServiceCollectionExtensions
{
    /// <summary>
    /// Adds FieldFlow options, HTTP client, mappers, and capability implementations.
    /// </summary>
    public static IServiceCollection AddFieldFlow(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<FieldFlowOptions>()
            .Bind(configuration.GetSection(FieldFlowOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
                "FieldFlow:BaseUrl must be an absolute URI.")
            .ValidateOnStart();

        services
            .AddOptions<FieldFlowResilienceOptions>()
            .Bind(configuration.GetSection(FieldFlowResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return AddFieldFlowCore(services);
    }

    /// <summary>
    /// Adds FieldFlow services when options are configured separately (tests).
    /// </summary>
    public static IServiceCollection AddFieldFlow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<FieldFlowOptions>();
        services.AddOptions<FieldFlowResilienceOptions>();
        return AddFieldFlowCore(services);
    }

    private static IServiceCollection AddFieldFlowCore(IServiceCollection services)
    {
        services.AddSingleton<FieldFlowContractorMapper>();
        services.AddSingleton<FieldFlowWorkOrderMapper>();
        services.AddSingleton<FieldFlowResilienceState>();
        services.AddSingleton<IConnectorRuntimeHealthSource, FieldFlowRuntimeHealthSource>();
        services.AddSingleton<IProviderCapabilities, FieldFlowProviderCapabilities>();
        services.AddSingleton<IWebhookVerifier, FieldFlowWebhookVerifier>();
        services.AddSingleton<IInboundWebhookNormalizer, FieldFlowWebhookNormalizer>();
        services.AddSingleton<IContractorSnapshotSource, FieldFlowContractorSnapshotSource>();
        services.AddSingleton<IWorkOrderSnapshotSource, FieldFlowWorkOrderSnapshotSource>();
        services.AddSingleton<IWorkOrderDispatcher, FieldFlowWorkOrderDispatcher>();
        services.AddSingleton<IWorkOrderReconciler, FieldFlowWorkOrderReconciler>();

        // Counting handler is the primary (innermost) handler so retries increment attempts.
        services.AddHttpClient<FieldFlowClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<FieldFlowOptions>>().Value;
                FieldFlowClient.ConfigureHttpClient(client, options);
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
                new FieldFlowAttemptCountingHandler(sp.GetRequiredService<FieldFlowResilienceState>())
                {
                    InnerHandler = new HttpClientHandler()
                })
            .AddResilienceHandler(FieldFlowResiliencePipeline.PipelineName, (builder, context) =>
                FieldFlowResiliencePipeline.Configure(builder, context.ServiceProvider));

        return services;
    }
}
