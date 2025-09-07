namespace HashiCorpIntegration.Data;

public interface IDynamicDbContextProvider
{
    IApplicationDbContext GetContext();
}
