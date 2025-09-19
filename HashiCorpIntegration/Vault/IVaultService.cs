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

    // KV Secret Management
    Task<Dictionary<string, object>> GetAllSecretsAsync(string path);
    Task<List<string>> ListSecretPathsAsync(string basePath = "");
    Task<bool> CreateOrUpdateSecretAsync(string path, Dictionary<string, object> secrets);
    Task<bool> CreateOrUpdateSecretKeyAsync(string path, string key, object value);
    Task<bool> DeleteSecretAsync(string path);
    Task<bool> DeleteSecretKeyAsync(string path, string key);
    void InvalidateKvCache(string path = null);
    Task<bool> SecretExistsAsync(string path);
    Task<Dictionary<string, object>> GetSecretMetadataAsync(string path);
    Task<Dictionary<string, object>> GetSecretVersionAsync(string path, int version);
    Task<bool> UndeleteSecretAsync(string path, int version);
    Task<bool> DestroySecretAsync(string path, int version);

    // Common methods
    Task<string> GetSecretAsync(string path, string key);
    Task<Dictionary<string, object>> GetSecretAsync(string path);
}