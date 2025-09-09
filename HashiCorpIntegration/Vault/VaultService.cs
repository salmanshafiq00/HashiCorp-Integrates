using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace HashiCorpIntegration.Vault;

public class VaultService(
    IOptions<VaultSettings> vaultSettings,
    IMemoryCache memoryCache,
    ILogger<VaultService> logger) : IVaultService
{
    private readonly VaultSettings _vaultSettings = vaultSettings.Value;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private const string CONNECTION_CACHE_KEY = "vault_connection_string";
    private const string LEASE_INFO_CACHE_KEY = "vault_lease_info";
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
        // Try to get from cache first
        if (_memoryCache.TryGetValue(CONNECTION_CACHE_KEY, out string? cachedConnectionString) &&
            !string.IsNullOrEmpty(cachedConnectionString))
        {
            logger.LogDebug("Using cached database connection string");
            return cachedConnectionString;
        }

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

            // Cache the connection string and lease info
            var leaseDuration = TimeSpan.FromSeconds(credentials.LeaseDurationSeconds);
            var cacheExpiry = DateTime.UtcNow.Add(leaseDuration).AddMinutes(-5); // Expire 5 minutes before lease expires

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = cacheExpiry,
                Priority = CacheItemPriority.High
            };

            _memoryCache.Set(CONNECTION_CACHE_KEY, connectionString, cacheOptions);

            // Store lease info for background service
            var leaseInfo = new LeaseInfo
            {
                LeaseId = credentials.LeaseId,
                LeaseDuration = leaseDuration,
                ExpiresAt = cacheExpiry
            };
            _memoryCache.Set(LEASE_INFO_CACHE_KEY, leaseInfo, cacheOptions);

            logger.LogInformation("Retrieved and cached dynamic database credentials. Expires at: {ExpiryTime}", cacheExpiry);
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
        _memoryCache.Remove(CONNECTION_CACHE_KEY);
        _memoryCache.Remove(LEASE_INFO_CACHE_KEY);
        logger.LogInformation("Connection string cache invalidated");
    }

    public LeaseInfo? GetCurrentLeaseInfo()
    {
        _memoryCache.TryGetValue(LEASE_INFO_CACHE_KEY, out LeaseInfo? leaseInfo);
        return leaseInfo;
    }

    public async Task<string> GetSecretAsync(string path, string key)
    {
        var cacheKey = $"vault_secret_{path}_{key}";

        // Try cache first
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedSecret) &&
            !string.IsNullOrEmpty(cachedSecret))
        {
            return cachedSecret;
        }

        try
        {
            var secret = await GetVaultClient().V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue(key, out var value))
            {
                var secretValue = value.ToString();

                // Cache static secrets for longer (use config setting)
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };

                _memoryCache.Set(cacheKey, secretValue, cacheOptions);
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

public class LeaseInfo
{
    public string LeaseId { get; set; } = string.Empty;
    public TimeSpan LeaseDuration { get; set; }
    public DateTime ExpiresAt { get; set; }
}