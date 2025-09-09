namespace HashiCorpIntegration.Vault;

public interface IVaultService
{
    Task<string> GetSqlConnectionStringAsync();
    Task<string> GetNewSqlConnectionStringAsync(); // Force new lease
    Task RenewLeaseAsync(string leaseId, int incrementSeconds = 3600);
    Task RevokeSingleLeaseAsync(string leaseId); // New method
    Task RevokeAllLeasesAsync(); // New method
    Task<List<LeaseInfo>> GetAllActiveLeasesAsync(); // New method
    void InvalidateConnectionCache();
    Task<string> GetSecretAsync(string path, string key);
    Task<Dictionary<string, object>> GetSecretAsync(string path);
    LeaseInfo? GetCurrentLeaseInfo();
}

