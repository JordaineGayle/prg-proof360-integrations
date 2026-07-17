using PRG.Proof360.Integrations.Api.Endpoints;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddFieldFlow();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapConnectorHealthEndpoints();

app.Run();
