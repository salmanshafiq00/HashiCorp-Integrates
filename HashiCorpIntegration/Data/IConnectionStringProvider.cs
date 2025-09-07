namespace HashiCorpIntegration.Data;

public interface IConnectionStringProvider
{
    Task<string> GetConnectionStringAsync();
}
