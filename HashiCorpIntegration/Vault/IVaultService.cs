using HashiCorpIntegration.src.Models;

namespace HashiCorpIntegration.Vault;

public interface IVaultService
{
    // Dynamic credential methods
    Task<string> GetSqlConnectionStringAsync();
    Task<string> GetNewSqlConnectionStringAsync();
    Task RenewLeaseAsync(string leaseId, int incrementSeconds = 3600);
    Task RevokeSingleLeaseAsync(string leaseId);
    Task RevokeAllLeasesAsync();
    Task<List<LeaseInfo>> GetAllActiveLeasesAsync();
    void InvalidateConnectionCache();
    LeaseInfo? GetCurrentLeaseInfo();

    // Static credential methods
    Task<string> GetStaticConnectionStringAsync();
    Task<StaticCredentialInfo?> GetStaticCredentialInfoAsync();
    Task<RotationInfo> RotateStaticCredentialsAsync();
    void InvalidateStaticConnectionCache();

    // Common methods
    Task<string> GetSecretAsync(string path, string key);
    Task<Dictionary<string, object>> GetSecretAsync(string path);
}