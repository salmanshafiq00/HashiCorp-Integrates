using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace HashiCorpIntegration.Vault;

public class VaultService : IVaultService
{
    private readonly VaultSettings _vaultSettings;
    private readonly ILogger<VaultService> _logger;
    private readonly IMemoryCache _cache;
    private readonly VaultClient _vaultClient;
    private readonly string _connectionCacheKey = "vault_db_connection";

    public VaultService(
        IOptions<VaultSettings> vaultSettings, 
        ILogger<VaultService> logger, 
        IMemoryCache cache)
    {
        _vaultSettings = vaultSettings.Value;
        _logger = logger;
        _cache = cache;
        _vaultClient = CreateVaultClient();
    }

    private VaultClient CreateVaultClient()
    {
        try
        {
            var vaultClientSettings = new VaultClientSettings(_vaultSettings.VaultUrl,
                new TokenAuthMethodInfo(_vaultSettings.VaultToken));
            return new VaultClient(vaultClientSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Vault client");
            throw;
        }
    }

    public async Task<string> GetSqlConnectionStringAsync()
    {
        //if (_cache.TryGetValue(_connectionCacheKey, out string cachedConnectionString))
        //{
        //    _logger.LogDebug("Retrieved database connection from cache");
        //    return cachedConnectionString;
        //}

        try
        {
            var credentials = await _vaultClient.V1.Secrets.Database
                .GetCredentialsAsync(_vaultSettings.DatabaseRole);

            var connectionString = $"Server={_vaultSettings.DatabaseServer};Database={_vaultSettings.DatabaseName};User Id={credentials.Data.Username};Password={credentials.Data.Password};TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30";

            // Clear EF Core connection pools
            SqlConnection.ClearAllPools();

            // Validate connection before caching
            if (!await ValidateConnectionAsync(connectionString))
            {
                throw new InvalidOperationException("Dynamic credentials are valid but database connection failed. Check user permissions.");
            }

            // Cache with shorter expiration than lease duration
            _cache.Set(_connectionCacheKey, connectionString, TimeSpan.FromMinutes(50));

            _logger.LogInformation("Retrieved and cached dynamic database credentials");
            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve dynamic database credentials from Vault");
            throw;
        }
    }

    public async Task RenewLeaseAsync(string leaseId, int incrementSeconds = 3600)
    {
        await _vaultClient.V1.System.RenewLeaseAsync(leaseId, incrementSeconds);
    }

    public void InvalidateConnectionCache()
    {
        _cache.Remove(_connectionCacheKey);
        _logger.LogInformation("Connection string cache invalidated");
    }

    public async Task<string> GetSecretAsync(string path, string key)
    {
        var cacheKey = $"vault_{path}_{key}";

        //if (_cache.TryGetValue(cacheKey, out string cachedValue))
        //{
        //    return cachedValue;
        //}

        try
        {
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue(key, out var value))
            {
                var secretValue = value.ToString();
                _cache.Set(cacheKey, secretValue, TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes));
                return secretValue;
            }
            throw new KeyNotFoundException($"Secret key '{key}' not found in path '{path}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret from Vault: {Path}/{Key}", path, key);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetSecretAsync(string path)
    {
        var cacheKey = $"vault_{path}_all";
        //if (_cache.TryGetValue(cacheKey, out Dictionary<string, object> cachedValue))
        //{
        //    return cachedValue;
        //}

        var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
        if (secret?.Data?.Data != null)
        {
            var secrets = new Dictionary<string, object>(secret.Data.Data);
            _cache.Set(cacheKey, secrets, TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes));
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

