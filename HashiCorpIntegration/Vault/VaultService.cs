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

    public VaultService(IOptions<VaultSettings> vaultSettings, ILogger<VaultService> logger, IMemoryCache cache)
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
            // Use Token authentication for dev mode
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

    public async Task<string> GetDynamicConnectionStringAsync()
    {
        const string cacheKey = "vault_db_connection";

        if (_cache.TryGetValue(cacheKey, out string cachedConnectionString))
        {
            _logger.LogDebug("Retrieved database connection from cache");
            return cachedConnectionString;
        }

        try
        {
            // Get dynamic database credentials
            var credentials = await _vaultClient.V1.Secrets.Database.GetCredentialsAsync(_vaultSettings.DatabaseRole);

            var username = credentials.Data.Username;
            var password = credentials.Data.Password;

            // Build connection string with dynamic credentials
            var connectionString = $"Server={_vaultSettings.DatabaseServer};Database={_vaultSettings.DatabaseName};User Id={username};Password={password};TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30";

            // Validate the connection before caching
            if (!await ValidateConnectionAsync(connectionString))
            {
                throw new InvalidOperationException("Dynamic credentials are valid but database connection failed. Check user permissions.");
            }

            // Cache for a shorter time than the lease duration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(50),
                Priority = CacheItemPriority.High
            };
            _cache.Set(cacheKey, connectionString, cacheOptions);

            _logger.LogInformation("Retrieved and cached dynamic database credentials");
            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve dynamic database credentials from Vault");
            throw;
        }
    }

    // Keep existing methods for backward compatibility
    public async Task<string> GetSecretAsync(string path, string key)
    {
        // Implementation stays the same...
        var cacheKey = $"vault_{path}_{key}";

        if (_cache.TryGetValue(cacheKey, out string cachedValue))
        {
            return cachedValue;
        }

        try
        {
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue(key, out var value))
            {
                var secretValue = value.ToString();
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes),
                    Priority = CacheItemPriority.High
                };
                _cache.Set(cacheKey, secretValue, cacheOptions);
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
        // Keep existing implementation...
        var cacheKey = $"vault_{path}_all";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, object> cachedValue))
        {
            return cachedValue;
        }

        var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
        if (secret?.Data?.Data != null)
        {
            var secrets = new Dictionary<string, object>(secret.Data.Data);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes),
                Priority = CacheItemPriority.High
            };
            _cache.Set(cacheKey, secrets, cacheOptions);
            return secrets;
        }
        throw new InvalidOperationException($"No secrets found in path '{path}'");
    }

    public async Task<string> GetSqlConnectionStringAsync()
    {
        // Use dynamic credentials instead of static secrets
        return await GetDynamicConnectionStringAsync();
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