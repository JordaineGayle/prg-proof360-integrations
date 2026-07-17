using Microsoft.OpenApi;

namespace PRG.Proof360.Integrations.Api.OpenApi;

/// <summary>Development OpenAPI / Swagger UI registration.</summary>
public static class SwaggerExtensions
{
    /// <summary>Registers Swagger generation for minimal API endpoints.</summary>
    public static IServiceCollection AddConnectorSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PRG.Proof360.Integrations API",
                Version = "v1",
                Description =
                    "Local FieldFlow connector prototype. " +
                    "Development also exposes /_demo/* helpers and the scenario runner at /_demo/scenarios."
            });
        });

        return services;
    }

    /// <summary>Maps Swagger JSON and UI (call only in Development).</summary>
    public static WebApplication UseConnectorSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "PRG.Proof360.Integrations v1");
            options.DocumentTitle = "Proof360 Integrations — Swagger";
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
