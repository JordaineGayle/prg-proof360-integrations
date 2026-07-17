using PRG.Proof360.Integrations.Api.Endpoints;
using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Api.OpenApi;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddFieldFlow(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddConnectorSwagger();
    builder.Services.AddHttpClient("demo-mock-proxy")
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<UnexpectedExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseConnectorSwagger();
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapConnectorHealthEndpoints();
app.MapSyncEndpoints();
app.MapWebhookEndpoints();
app.MapDispatchEndpoints();
app.MapAdminReplayEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapDemoSeedEndpoints();
    app.MapDemoMockProxyEndpoints();
    app.MapGet("/_demo/scenarios", () => Results.Redirect("/_demo/scenarios.html"))
        .WithTags("Demo")
        .WithSummary("Open the interactive scenario runner");
}

app.Run();
