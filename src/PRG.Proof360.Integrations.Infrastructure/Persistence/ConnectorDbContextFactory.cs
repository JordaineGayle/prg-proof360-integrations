using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations</c>.
/// </summary>
public sealed class ConnectorDbContextFactory : IDesignTimeDbContextFactory<ConnectorDbContext>
{
    /// <inheritdoc />
    public ConnectorDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConnectorDbContext>()
            .UseSqlite("Data Source=connector.design.db")
            .Options;

        return new ConnectorDbContext(options);
    }
}
