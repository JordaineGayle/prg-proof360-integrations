using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.FieldFlow;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Development-only same-origin proxy to the FieldFlow mock <c>/_test</c> surface.
/// Lets the browser scenario runner avoid cross-origin / localhost-vs-127.0.0.1 failures.
/// </summary>
public static class DemoMockProxyEndpoints
{
    /// <summary>Maps <c>/_demo/proxy/mock/{**path}</c>.</summary>
    public static IEndpointRouteBuilder MapDemoMockProxyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(
                "/_demo/proxy/mock/{**path}",
                ["GET", "POST", "PUT", "PATCH", "DELETE"],
                async (
                    string path,
                    HttpContext http,
                    IHttpClientFactory httpClientFactory,
                    IOptions<FieldFlowOptions> options,
                    CancellationToken cancellationToken) =>
                {
                    var allowed = path.Equals("health", StringComparison.OrdinalIgnoreCase) ||
                                  path.StartsWith("_test/", StringComparison.OrdinalIgnoreCase);
                    if (string.IsNullOrWhiteSpace(path) || !allowed)
                    {
                        return Results.BadRequest(new
                        {
                            error = "Only mock /health and /_test/* paths may be proxied."
                        });
                    }

                    var baseUrl = options.Value.BaseUrl.TrimEnd('/') + "/";
                    var target = new Uri(new Uri(baseUrl, UriKind.Absolute), path + http.Request.QueryString);

                    var client = httpClientFactory.CreateClient("demo-mock-proxy");
                    using var upstream = new HttpRequestMessage(new HttpMethod(http.Request.Method), target);

                    if (http.Request.ContentLength is > 0 ||
                        !string.IsNullOrWhiteSpace(http.Request.ContentType))
                    {
                        using var ms = new MemoryStream();
                        await http.Request.Body.CopyToAsync(ms, cancellationToken);
                        upstream.Content = new ByteArrayContent(ms.ToArray());
                        if (http.Request.ContentType is { } requestContentType)
                        {
                            upstream.Content.Headers.TryAddWithoutValidation("Content-Type", requestContentType);
                        }
                    }

                    using var response = await client.SendAsync(upstream, cancellationToken);
                    var bytes = response.Content is null
                        ? Array.Empty<byte>()
                        : await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var responseContentType = response.Content?.Headers.ContentType?.ToString() ?? "application/json";

                    return Results.Content(
                        System.Text.Encoding.UTF8.GetString(bytes),
                        responseContentType,
                        statusCode: (int)response.StatusCode);
                })
            .WithTags("Demo")
            .WithSummary("Same-origin proxy to FieldFlow mock /health and /_test/* (Development only)");

        return endpoints;
    }
}
