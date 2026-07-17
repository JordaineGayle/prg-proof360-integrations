using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// Applies assignment-exact column names from <see cref="CanonicalFieldAttribute"/> metadata.
/// </summary>
internal static class CanonicalFieldMapping
{
    /// <summary>
    /// Maps every annotated property to its exact canonical column name.
    /// </summary>
    public static void MapCanonicalColumns<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        foreach (var property in typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<CanonicalFieldAttribute>();
            if (attribute is null)
            {
                continue;
            }

            builder.Property(property.Name).HasColumnName(attribute.Name);
        }
    }
}
