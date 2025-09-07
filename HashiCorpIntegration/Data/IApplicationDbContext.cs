using HashiCorpIntegration.Entities;
using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Data;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    int SaveChanges();
}
