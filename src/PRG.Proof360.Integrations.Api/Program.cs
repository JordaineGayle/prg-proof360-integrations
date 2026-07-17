using PRG.Proof360.Integrations.Api.Endpoints;
using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddFieldFlow(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<UnexpectedExceptionMiddleware>();

app.MapConnectorHealthEndpoints();
app.MapSyncEndpoints();
app.MapWebhookEndpoints();
app.MapDispatchEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapDemoSeedEndpoints();
}

app.Run();
