using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PRG.Proof360.Integrations.FieldFlow;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.FieldFlow.Resilience;

namespace PRG.Proof360.Integrations.ResilienceTests.Support;

/// <summary>DI host for FieldFlow resilience tests with a scripted transport.</summary>
public sealed class ResilienceTestFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private ResilienceTestFixture(ServiceProvider provider, ScriptedHttpMessageHandler handler, FakeTimeProvider time, FieldFlowResilienceState state)
    {
        _provider = provider;
        Handler = handler;
        Time = time;
        State = state;
        Client = provider.GetRequiredService<FieldFlowClient>();
        Options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FieldFlowResilienceOptions>>().Value;
    }

    /// <summary>Scripted handler.</summary>
    public ScriptedHttpMessageHandler Handler { get; }

    /// <summary>Fake time provider.</summary>
    public FakeTimeProvider Time { get; }

    /// <summary>Process resilience state.</summary>
    public FieldFlowResilienceState State { get; }

    /// <summary>Typed client under test.</summary>
    public FieldFlowClient Client { get; }

    /// <summary>Active resilience options.</summary>
    public FieldFlowResilienceOptions Options { get; }

    /// <summary>Creates a fixture with test-friendly resilience thresholds.</summary>
    public static ResilienceTestFixture Create(Action<FieldFlowResilienceOptions>? configure = null)
    {
        var handler = new ScriptedHttpMessageHandler();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time);
        services.AddLogging();
        services.Configure<FieldFlowOptions>(o =>
        {
            o.BaseUrl = "http://fieldflow.test/";
            o.ApiKey = "test-key";
            o.ProviderInstanceId = "fieldflow-resilience-1";
            o.TimeoutMilliseconds = 120_000;
        });
        services.Configure<FieldFlowResilienceOptions>(o =>
        {
            o.AttemptTimeoutMilliseconds = 200;
            o.MaxRetryAttempts = 3;
            o.BaseDelayMilliseconds = 1_000;
            o.MaxDelayMilliseconds = 5_000;
            o.MaxRetryAfterMilliseconds = 60_000;
            o.DisableRetryDelays = true;
            o.CircuitFailureRatio = 1.0;
            // Keep well above MaxRetryAttempts so a single logical call cannot open the circuit mid-retry.
            o.CircuitMinimumThroughput = 20;
            o.CircuitSamplingDurationSeconds = 60;
            o.CircuitBreakDurationSeconds = 30;
            o.ConcurrencyLimit = 16;
            o.ConcurrencyQueueLimit = 16;
            configure?.Invoke(o);
        });
        services.AddFieldFlow();
        services.AddHttpClient<FieldFlowClient>()
            .ConfigurePrimaryHttpMessageHandler(sp =>
                new FieldFlowAttemptCountingHandler(sp.GetRequiredService<FieldFlowResilienceState>())
                {
                    InnerHandler = handler
                });

        var provider = services.BuildServiceProvider();
        return new ResilienceTestFixture(
            provider,
            handler,
            time,
            provider.GetRequiredService<FieldFlowResilienceState>());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _provider.Dispose();
        return ValueTask.CompletedTask;
    }
}
