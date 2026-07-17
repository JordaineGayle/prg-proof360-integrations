using Microsoft.AspNetCore.Http.Features;
using PRG.FieldFlow.Mock.Endpoints;
using PRG.FieldFlow.Mock.Middleware;
using PRG.FieldFlow.Mock.Options;
using PRG.FieldFlow.Mock.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FieldFlowMockOptions>(builder.Configuration.GetSection(FieldFlowMockOptions.SectionName));
builder.Services.AddSingleton<MockStore>();
builder.Services.AddHttpClient("webhook-demo");
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = builder.Configuration.GetSection(FieldFlowMockOptions.SectionName)
        .GetValue(nameof(FieldFlowMockOptions.MaxRequestBodyBytes), 64 * 1024);
});
builder.WebHost.ConfigureKestrel(options =>
{
    var max = builder.Configuration.GetSection(FieldFlowMockOptions.SectionName)
        .GetValue(nameof(FieldFlowMockOptions.MaxRequestBodyBytes), 64 * 1024);
    options.Limits.MaxRequestBodySize = max;
});

var app = builder.Build();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<FailureInjectionMiddleware>();

app.MapProviderEndpoints();
app.MapTestControlEndpoints();

app.Run();

/// <summary>Exposes the mock entry point to WebApplicationFactory hosts.</summary>
public partial class Program;
