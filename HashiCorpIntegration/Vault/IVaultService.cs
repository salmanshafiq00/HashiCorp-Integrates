namespace HashiCorpIntegration.Vault;

public interface IVaultService
{
    Task<string> GetSqlConnectionStringAsync();
    Task RenewLeaseAsync(string leaseId, int incrementSeconds = 3600);
    void InvalidateConnectionCache();
    Task<string> GetSecretAsync(string path, string key);
    Task<Dictionary<string, object>> GetSecretAsync(string path);
    LeaseInfo? GetCurrentLeaseInfo(); // Add this method
}