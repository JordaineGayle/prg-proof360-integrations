using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PRG.Proof360.Integrations.IntegrationTests.FieldFlowMock;

public sealed class FieldFlowMockEndpointTests : IAsyncLifetime
{
    private FieldFlowMockFactory _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        _factory = new FieldFlowMockFactory();
        _client = _factory.CreateAuthenticatedClient();
        _anonymous = _factory.CreateClient();
        using var reset = await _anonymous.PostAsync(new Uri("/_test/reset", UriKind.Relative), content: null);
        reset.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _anonymous.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_returns_healthy()
    {
        using var response = await _anonymous.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Contractors_and_work_orders_list_normal_behavior()
    {
        using var contractors = await _client.GetAsync(new Uri("/contractors", UriKind.Relative));
        using var workOrders = await _client.GetAsync(new Uri("/work-orders", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, contractors.StatusCode);
        Assert.Equal(HttpStatusCode.OK, workOrders.StatusCode);

        var contractorList = await contractors.Content.ReadFromJsonAsync<JsonElement>();
        var workOrderList = await workOrders.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(contractorList.GetArrayLength() >= 2);
        Assert.True(workOrderList.GetArrayLength() >= 3);

        using var single = await _client.GetAsync(new Uri("/work-orders/wo-2001", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task Api_key_failure_returns_401()
    {
        using var response = await _anonymous.GetAsync(new Uri("/contractors", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        Assert.Equal("unauthorized", doc.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(FieldFlowMockFactory.ApiKey, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_work_order_idempotency_repeat_and_conflict()
    {
        var body = CreateBody("job-idem-1");
        using var first = await PostCreateAsync("key-1", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<WorkOrderResponse>();
        Assert.NotNull(created);
        Assert.Equal("job-idem-1", created.ClientReference);

        using var replay = await PostCreateAsync("key-1", body);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<WorkOrderResponse>();
        Assert.Equal(created.WorkOrderId, replayed!.WorkOrderId);

        var different = CreateBody("job-idem-1") with { CustomerName = "Different Name" };
        using var conflict = await PostCreateAsync("key-1", different);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Patch_status_updates_entity_version()
    {
        using var response = await _client.PatchAsJsonAsync(
            new Uri("/work-orders/wo-2001/status", UriKind.Relative),
            new { status = "scheduled" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<WorkOrderResponse>();
        Assert.Equal("scheduled", updated!.Status);
        Assert.Equal(2, updated.EntityVersion);
    }

    [Fact]
    public async Task Failure_modes_429_500_timeout_and_health()
    {
        using var configure = await _anonymous.PostAsJsonAsync(
            new Uri("/_test/failures", UriKind.Relative),
            new
            {
                rateLimitCount = 1,
                retryAfterSeconds = 3,
                serverErrorCount = 1,
                timeoutCount = 1,
                timeoutDelayMilliseconds = 50,
                healthUnavailable = true
            });
        configure.EnsureSuccessStatusCode();

        using var rateLimited = await _client.GetAsync(new Uri("/contractors", UriKind.Relative));
        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
        Assert.Equal("3", rateLimited.Headers.GetValues("Retry-After").First());

        using var serverError = await _client.GetAsync(new Uri("/contractors", UriKind.Relative));
        Assert.Equal(HttpStatusCode.InternalServerError, serverError.StatusCode);

        using var timeout = await _client.GetAsync(new Uri("/contractors", UriKind.Relative));
        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode);

        using var health = await _anonymous.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
    }

    [Fact]
    public async Task Ambiguous_post_can_be_reconciled_by_client_reference()
    {
        using var arm = await _anonymous.PostAsJsonAsync(
            new Uri("/_test/failures", UriKind.Relative),
            new { ambiguousNextPost = true });
        arm.EnsureSuccessStatusCode();

        var body = CreateBody("job-ambiguous-1");
        var exceptionThrown = false;
        try
        {
            using var dropped = await PostCreateAsync("key-ambiguous", body);
            _ = dropped.StatusCode;
        }
        catch (HttpRequestException)
        {
            exceptionThrown = true;
        }
        catch (IOException)
        {
            exceptionThrown = true;
        }
        catch (OperationCanceledException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);

        using var reconcile = await _anonymous.GetAsync(
            new Uri("/_test/work-orders/by-client-ref/job-ambiguous-1", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, reconcile.StatusCode);
        var found = await reconcile.Content.ReadFromJsonAsync<WorkOrderResponse>();
        Assert.Equal("job-ambiguous-1", found!.ClientReference);
        Assert.False(string.IsNullOrWhiteSpace(found.WorkOrderId));
    }

    [Fact]
    public async Task Webhook_signing_fixture_is_reproducible()
    {
        var request = new
        {
            workOrderId = "wo-2001",
            eventId = "evt-fixed-001",
            eventType = "work_order.status_changed",
            occurredAt = "2026-07-17T12:00:00Z"
        };

        using var first = await _anonymous.PostAsJsonAsync(new Uri("/_test/webhooks/build", UriKind.Relative), request);
        using var second = await _anonymous.PostAsJsonAsync(new Uri("/_test/webhooks/build", UriKind.Relative), request);
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        var a = await first.Content.ReadFromJsonAsync<WebhookBuildResponse>();
        var b = await second.Content.ReadFromJsonAsync<WebhookBuildResponse>();
        Assert.Equal(a!.Headers["X-FieldFlow-Signature"], b!.Headers["X-FieldFlow-Signature"]);
        Assert.Equal("evt-fixed-001", a.Headers["X-FieldFlow-Event-Id"]);

        var raw = Convert.FromBase64String(a.RawBodyBase64);
        var unix = long.Parse(a.Headers["X-FieldFlow-Timestamp"]);
        var expected = Sign(FieldFlowMockFactory.WebhookSecret, unix, raw);
        Assert.Equal(expected, a.Headers["X-FieldFlow-Signature"]);
    }

    [Fact]
    public async Task Repeated_and_out_of_order_webhook_events_can_be_built()
    {
        using var high = await _anonymous.PostAsJsonAsync(
            new Uri("/_test/webhooks/build", UriKind.Relative),
            new { workOrderId = "wo-2001", eventId = "evt-order-high", entityVersion = 5, status = "done" });
        using var low = await _anonymous.PostAsJsonAsync(
            new Uri("/_test/webhooks/build", UriKind.Relative),
            new { workOrderId = "wo-2001", eventId = "evt-order-low", entityVersion = 2, status = "scheduled" });
        using var dup = await _anonymous.PostAsJsonAsync(
            new Uri("/_test/webhooks/build", UriKind.Relative),
            new { workOrderId = "wo-2001", eventId = "evt-order-high", entityVersion = 5, status = "done" });

        high.EnsureSuccessStatusCode();
        low.EnsureSuccessStatusCode();
        dup.EnsureSuccessStatusCode();

        var highBody = await high.Content.ReadFromJsonAsync<WebhookBuildResponse>();
        var lowBody = await low.Content.ReadFromJsonAsync<WebhookBuildResponse>();
        Assert.Equal("5", highBody!.Headers["X-FieldFlow-Entity-Version"]);
        Assert.Equal("2", lowBody!.Headers["X-FieldFlow-Entity-Version"]);
        Assert.Equal("evt-order-high", (await dup.Content.ReadFromJsonAsync<WebhookBuildResponse>())!.Headers["X-FieldFlow-Event-Id"]);
    }

    [Fact]
    public async Task Additive_optional_field_is_present_on_fixture()
    {
        using var response = await _client.GetAsync(new Uri("/work-orders/wo-2100", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("demo-additive-field", doc.RootElement.GetProperty("unexpectedOptionalTag").GetString());
    }

    [Fact]
    public async Task Unknown_contractor_work_order_is_not_in_contractor_list()
    {
        using var wo = await _client.GetAsync(new Uri("/work-orders/wo-2099", UriKind.Relative));
        using var contractors = await _client.GetAsync(new Uri("/contractors", UriKind.Relative));
        wo.EnsureSuccessStatusCode();
        contractors.EnsureSuccessStatusCode();

        var workOrder = await wo.Content.ReadFromJsonAsync<WorkOrderResponse>();
        var list = await contractors.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ctr-missing-999", workOrder!.ContractorId);
        Assert.DoesNotContain(
            list.EnumerateArray().Select(x => x.GetProperty("contractorId").GetString()),
            id => id == "ctr-missing-999");
    }

    [Fact]
    public async Task Reset_provides_test_isolation()
    {
        var body = CreateBody("job-reset-1");
        using var created = await PostCreateAsync("key-reset", body);
        created.EnsureSuccessStatusCode();

        using var reset = await _anonymous.PostAsync(new Uri("/_test/reset", UriKind.Relative), content: null);
        reset.EnsureSuccessStatusCode();

        using var missing = await _anonymous.GetAsync(
            new Uri("/_test/work-orders/by-client-ref/job-reset-1", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var fixture = await _client.GetAsync(new Uri("/work-orders/wo-2001", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, fixture.StatusCode);
    }

    private async Task<HttpResponseMessage> PostCreateAsync(string idempotencyKey, CreateWorkOrderBody body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/work-orders", UriKind.Relative))
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(request);
    }

    private static CreateWorkOrderBody CreateBody(string clientReference) => new(
        clientReference,
        "ctr-1001",
        "Ada Fixture",
        "+1-555-0100",
        "ada.fixture@example.test",
        "100 Mock Street",
        null,
        "Calgary",
        "T2P1J9",
        "plumbing",
        "leak",
        DateTimeOffset.Parse("2026-08-01T15:00:00Z"),
        DateTimeOffset.Parse("2026-08-01T17:00:00Z"),
        "notes");

    private static string Sign(string secret, long unixSeconds, ReadOnlySpan<byte> rawBody)
    {
        var prefix = Encoding.UTF8.GetBytes($"{unixSeconds}.");
        var buffer = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(buffer);
        rawBody.CopyTo(buffer.AsSpan(prefix.Length));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(buffer)).ToLowerInvariant();
    }

    private sealed record CreateWorkOrderBody(
        [property: JsonPropertyName("clientReference")] string ClientReference,
        [property: JsonPropertyName("contractorId")] string? ContractorId,
        [property: JsonPropertyName("customerName")] string CustomerName,
        [property: JsonPropertyName("customerPhone")] string? CustomerPhone,
        [property: JsonPropertyName("customerEmail")] string? CustomerEmail,
        [property: JsonPropertyName("addressStreet")] string AddressStreet,
        [property: JsonPropertyName("addressUnit")] string? AddressUnit,
        [property: JsonPropertyName("addressCity")] string AddressCity,
        [property: JsonPropertyName("addressPostal")] string? AddressPostal,
        [property: JsonPropertyName("serviceType")] string ServiceType,
        [property: JsonPropertyName("subcategory")] string? Subcategory,
        [property: JsonPropertyName("windowStart")] DateTimeOffset? WindowStart,
        [property: JsonPropertyName("windowEnd")] DateTimeOffset? WindowEnd,
        [property: JsonPropertyName("notes")] string? Notes);

    private sealed record WorkOrderResponse(
        [property: JsonPropertyName("workOrderId")] string WorkOrderId,
        [property: JsonPropertyName("contractorId")] string? ContractorId,
        [property: JsonPropertyName("clientReference")] string? ClientReference,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("entityVersion")] long EntityVersion);

    private sealed record WebhookBuildResponse(
        [property: JsonPropertyName("headers")] Dictionary<string, string> Headers,
        [property: JsonPropertyName("rawBodyBase64")] string RawBodyBase64);
}
