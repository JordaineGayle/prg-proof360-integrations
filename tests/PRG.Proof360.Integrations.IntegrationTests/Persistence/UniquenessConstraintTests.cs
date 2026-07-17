using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.IntegrationTests.Persistence;

public sealed class UniquenessConstraintTests
{
    [Fact]
    public async Task Inbox_event_id_is_unique_per_provider_instance()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.DbContext;

        db.InboxMessages.Add(CreateInbox("evt-1"));
        await db.SaveChangesAsync();

        db.InboxMessages.Add(CreateInbox("evt-1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Outbox_idempotency_key_is_unique_per_provider_instance()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.DbContext;

        db.OutboxMessages.Add(CreateOutbox("key-1"));
        await db.SaveChangesAsync();

        db.OutboxMessages.Add(CreateOutbox("key-1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task External_identity_is_unique_per_provider_instance()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.DbContext;

        db.ProviderIdentityLinks.Add(CreateLink(Guid.NewGuid(), "ext-1"));
        await db.SaveChangesAsync();

        db.ProviderIdentityLinks.Add(CreateLink(Guid.NewGuid(), "ext-1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_duplicate_identity_inserts_cannot_create_two_active_mappings()
    {
        // Shared file DB so competing contexts hit the same unique index.
        var path = Path.Combine(Path.GetTempPath(), $"prg-identity-{Guid.NewGuid():N}.db");
        try
        {
            await using (var setup = CreateFileContext(path))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var exceptions = 0;
            var tasks = Enumerable.Range(0, 8).Select(async _ =>
            {
                try
                {
                    await using var context = CreateFileContext(path);
                    context.ProviderIdentityLinks.Add(CreateLink(Guid.NewGuid(), "contractor-concurrent"));
                    await context.SaveChangesAsync();
                }
                catch (Exception ex) when (ex is DbUpdateException || ex.InnerException is not null)
                {
                    Interlocked.Increment(ref exceptions);
                }
            });

            await Task.WhenAll(tasks);

            await using var verify = CreateFileContext(path);
            var count = await verify.ProviderIdentityLinks.CountAsync(x => x.ExternalId == "contractor-concurrent");
            Assert.Equal(1, count);
            Assert.True(exceptions >= 1);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Canonical_vendor_persists_without_provider_payload_columns()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var vendor = new Vendor
        {
            VendorId = Guid.NewGuid(),
            Status = VendorStatuses.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow
        };

        database.DbContext.Vendors.Add(vendor);
        await database.DbContext.SaveChangesAsync();

        var columns = database.DbContext.Model.FindEntityType(typeof(Vendor))!
            .GetProperties()
            .Select(property => property.GetColumnName())
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("payload", columns);
        Assert.DoesNotContain("external_id", columns);
        Assert.Single(await database.DbContext.Vendors.ToListAsync());
    }

    private static ConnectorDbContext CreateFileContext(string path)
    {
        var options = new DbContextOptionsBuilder<ConnectorDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new ConnectorDbContext(options);
    }

    private static InboxMessage CreateInbox(string eventId) => new()
    {
        Id = Guid.NewGuid(),
        ProviderName = "FieldFlow",
        ProviderInstanceId = "instance-1",
        EventId = eventId,
        EventType = "work_order.updated",
        OccurredAt = DateTimeOffset.UtcNow,
        ReceivedAt = DateTimeOffset.UtcNow,
        PayloadEnvelope = "{}",
        PayloadHash = "hash",
        State = InboxMessageStates.Pending
    };

    private static OutboxMessage CreateOutbox(string key) => new()
    {
        Id = Guid.NewGuid(),
        ProviderName = "FieldFlow",
        ProviderInstanceId = "instance-1",
        OperationType = "DispatchWorkOrder",
        IdempotencyKey = key,
        CanonicalEntityType = "Job",
        CanonicalId = Guid.NewGuid(),
        CommandPayload = "{}",
        State = OutboxMessageStates.Pending,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static ProviderIdentityLink CreateLink(Guid canonicalId, string externalId) => new()
    {
        Id = Guid.NewGuid(),
        ProviderName = "FieldFlow",
        ProviderInstanceId = "instance-1",
        ExternalEntityType = "Contractor",
        ExternalId = externalId,
        CanonicalEntityType = "Vendor",
        CanonicalId = canonicalId
    };
}
