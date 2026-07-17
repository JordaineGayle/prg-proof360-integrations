using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.Infrastructure.DependencyInjection;

namespace PRG.Proof360.Integrations.ResilienceTests.Smoke;

public sealed class ScaffoldSmokeTests
{
    [Fact]
    public void FieldFlow_and_infrastructure_registration_extensions_are_callable()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectorPersistence:ConnectionString"] = "Data Source=:memory:",
            ["ConnectorPersistence:ApplyMigrationsOnStartup"] = "false"
        }).Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddFieldFlow();
        services.AddInfrastructure(configuration);

        Assert.NotNull(services);
    }
}
