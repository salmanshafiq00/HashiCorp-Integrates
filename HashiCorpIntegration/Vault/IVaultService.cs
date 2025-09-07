namespace HashiCorpIntegration.Vault;

public interface IVaultService
{
    Task<string> GetSecretAsync(string path, string key);
    Task<Dictionary<string, object>> GetSecretAsync(string path);
    Task<string> GetSqlConnectionStringAsync();
    Task<string> GetDynamicConnectionStringAsync(); // New method for dynamic DB credentials
}