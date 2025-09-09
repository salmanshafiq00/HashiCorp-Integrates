using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace HashiCorpIntegration.Vault;

public class VaultService(
    IOptions<VaultSettings> vaultSettings,
    ILogger<VaultService> logger) : IVaultService
{
    private readonly VaultSettings _vaultSettings = vaultSettings.Value;
    private VaultClient? _vaultClient;

    private VaultClient GetVaultClient()
    {
        if (_vaultClient == null)
        {
            try
            {
                var vaultClientSettings = new VaultClientSettings(_vaultSettings.VaultUrl,
                    new TokenAuthMethodInfo(_vaultSettings.VaultToken));
                _vaultClient = new VaultClient(vaultClientSettings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Vault client");
                throw;
            }
        }
        return _vaultClient;
    }

    public async Task<string> GetSqlConnectionStringAsync()
    {
        try
        {
            var credentials = await GetVaultClient().V1.Secrets.Database
                .GetCredentialsAsync(_vaultSettings.DatabaseRole);

            var connectionString = $"Server={_vaultSettings.DatabaseServer};Database={_vaultSettings.DatabaseName};User Id={credentials.Data.Username};Password={credentials.Data.Password};TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30";

            // Clear EF Core connection pools
            SqlConnection.ClearAllPools();

            // Validate connection before caching
            if (!await ValidateConnectionAsync(connectionString))
            {
                throw new InvalidOperationException("Dynamic credentials are valid but database connection failed. Check user permissions.");
            }
            logger.LogInformation("Retrieved and cached dynamic database credentials");
            return connectionString;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve dynamic database credentials from Vault");
            throw;
        }
    }

    public async Task RenewLeaseAsync(string leaseId, int incrementSeconds = 3600)
    {
        await GetVaultClient().V1.System.RenewLeaseAsync(leaseId, incrementSeconds);
    }

    public void InvalidateConnectionCache()
    {
        logger.LogInformation("Connection string cache invalidated");
    }

    public async Task<string> GetSecretAsync(string path, string key)
    {
        var cacheKey = $"vault_{path}_{key}";

        try
        {
            var secret = await GetVaultClient().V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue(key, out var value))
            {
                var secretValue = value.ToString();
                return secretValue;
            }
            throw new KeyNotFoundException($"Secret key '{key}' not found in path '{path}'");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve secret from Vault: {Path}/{Key}", path, key);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetSecretAsync(string path)
    {
        var secret = await GetVaultClient().V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
        if (secret?.Data?.Data != null)
        {
            var secrets = new Dictionary<string, object>(secret.Data.Data);
            return secrets;
        }
        throw new InvalidOperationException($"No secrets found in path '{path}'");
    }

    private async Task<bool> ValidateConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

