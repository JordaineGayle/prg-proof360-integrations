using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.IntegrationTests.Persistence;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection, ConnectorDbContext dbContext)
    {
        _connection = connection;
        DbContext = dbContext;
    }

    public ConnectorDbContext DbContext { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ConnectorDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ConnectorDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new SqliteTestDatabase(connection, dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
