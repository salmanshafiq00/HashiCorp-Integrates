namespace HashiCorpIntegration.Vault;

public interface IVaultService
{
    Task<string> GetSecretAsync(string path, string key);
    Task<Dictionary<string, object>> GetSecretAsync(string path);
    Task<string> GetSqlConnectionStringAsync();
    void InvalidateConnectionCache(); // New method for cache invalidation
}