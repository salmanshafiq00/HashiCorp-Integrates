namespace HashiCorpIntegration.Data;

public interface IApplicationDbContextFactory
{
    Task<ApplicationDbContext> CreateDbContextAsync();
}